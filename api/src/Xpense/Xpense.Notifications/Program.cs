using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xpense.Notifications;
using Xpense.Notifications.Rules;
using Xpense.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<XpenseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddNotificationRules();

builder.Services.AddScoped<EventProcessor>();
builder.Services.AddHostedService<EventPump>();

await builder.Build().RunAsync();
