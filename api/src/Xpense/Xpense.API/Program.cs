using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.API.Extensions.cs;

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
builder.Services.AddControllers();
builder.Services.ConfigureSwagger();
builder.Services.ConfigurePersistence(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
builder.Services.ConfigureApiVersioning();

var app = builder.Build();

app.UseStaticFiles("/static");
app.UseRouting();
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