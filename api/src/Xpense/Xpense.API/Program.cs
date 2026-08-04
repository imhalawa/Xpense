using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Entities;

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

builder.Services.AddSingleton(Log.Logger);
// AddControllers used to bring these in implicitly; without MVC they must be explicit.
builder.Services.AddCors();
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger();
builder.Services.ConfigurePersistence(builder.Configuration);
builder.Services.AddDomainServices();
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

// Every slice, discovered by scanning for IEndpoint. There is no per-feature registration.
app.MapEndpoints();

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
