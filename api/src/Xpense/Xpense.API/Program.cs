using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions.cs;
using Xpense.API.Filters;
using Xpense.Persistence;
using Xpense.Services.Entities;

var builder = WebApplication.CreateBuilder(args);

// Activate Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// Register Services
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>()).AddNewtonsoftJson();
builder.Services.ConfigureSwagger();
builder.Services.ConfigurePersistence(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddUseCases();
builder.Services.ConfigureApiVersioning();
builder.Services.AddRequestValidation();
builder.Services.AddExceptionHandlers();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<XpenseDbContext>();
    context.Database.EnsureCreated();
    Seeder.Seed<Priority>(context, "Priorities.json");
}

// First in the pipeline so it wraps everything downstream. The registered IExceptionHandlers
// decide the status and body; anything unclaimed falls through to FallbackExceptionHandler.
app.UseExceptionHandler();

app.UseStaticFiles("/static");
app.UseRouting();
app.UseCors(policy =>
{
    // TODO: later you need to find out how to distinguish between Local/Dev/Test/Production
    policy.WithOrigins("http://localhost:5173");
    policy.AllowAnyHeader();
    policy.AllowAnyMethod();
});
app.MapControllers();

// Enable Swagger & Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
        options.InjectStylesheet("/static/styles/swagger-ui.css");
    });
}

app.Run();

public partial class Program;
