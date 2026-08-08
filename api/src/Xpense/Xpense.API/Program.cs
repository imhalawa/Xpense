using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddSingleton<Serilog.ILogger>(_ => Log.Logger);
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger();
builder.Services.ConfigurePersistence(builder.Configuration);
builder.Services.AddXpenseForwardedHeaders(builder.Configuration);
builder.Services.AddXpenseAuthentication(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddRequestValidation();
builder.Services.AddExceptionHandlers();
builder.Services.AddHealthProbe();

var app = builder.Build();


app.UseExceptionHandler();
app.UseForwardedHeaders();

app.UseStaticFiles("/static");
app.UseRouting();
app.UseCors(AuthenticationPolicyNames.Cors);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapEndpoints();

app.MapHealthChecks("/health");

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
