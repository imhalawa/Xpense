using System;
using System.IO;
using System.Reflection;
using Castle.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using Xpense.API.Data;

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

// Register Controllers
builder.Services.AddControllers();

// Register XpenseDbContext, XpenseDbContextFactory
builder.Services.AddPooledDbContextFactory<XpenseDbContext>(optionsBuilder =>
{
    optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Register Swagger Generation Services
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo()
    {
        Version = "v1",
        Title = "Xpense",
        Description = "Financial Tracking Services and Advisory",
        // TODO: Add Terms of Use
        Contact = new OpenApiContact()
        {
            Name = "Mohamed Halawa",
            Email = "imhalawa@outlook.com",
            Url = new Uri("https://halawa.dev/about")
        },
    });

    // Read XML Comments Generated Document
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Register API Versioning Services, see https://github.com/dotnet/aspnet-api-versioning/wiki
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("x-xpns-version");
});

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