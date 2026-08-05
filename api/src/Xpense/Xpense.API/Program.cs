using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions;
using Xpense.API.Infrastructure;

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
builder.Services.AddHealthProbe();

var app = builder.Build();

// Nothing touches the database during startup. Migrations are a deployment step, applied by the
// one-shot `migrations` container before this one starts, and the Priority reference data is part
// of that schema rather than a boot-time write. See docs/adr/0004 and docs/adr/0005.
// Locally, run `dotnet ef database update` before starting the API.

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

// Deliberately not a slice: /health is infrastructure, not a feature, and has no contract to
// version. It is the one route mapped outside Features/ -- see the carve-out in AGENTS.md.
app.MapHealthChecks("/health");

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
