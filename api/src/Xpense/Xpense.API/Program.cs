using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions.cs;
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
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.ConfigureSwagger();
builder.Services.ConfigurePersistence(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddUseCases();
builder.Services.ConfigureApiVersioning();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<XpenseDbContext>();
    context.Database.EnsureCreated();

    Seeder.Seed<Priority>(context, "Priorities.json");
}

app.UseStaticFiles("/static");
app.UseRouting();
app.MapControllers();
app.UseGlobalExceptionHandler();

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