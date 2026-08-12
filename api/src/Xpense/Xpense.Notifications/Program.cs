using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Xpense.Notifications.Email;
using Xpense.Notifications.Rules;
using Xpense.Persistence;

namespace Xpense.Notifications;

public static class NotificationsProgram
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console());

        builder.Services.AddDbContext<XpenseDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddNotificationRules();

        var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"]
            ?? throw new InvalidOperationException("The data-protection key directory is required.");

        builder.Services.AddDataProtection()
            .SetApplicationName("Xpense")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

        builder.Services.AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        builder.Services.AddSingleton<ISmtpTransport, SmtpTransport>();
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.AddScoped<EventProcessor>();
        builder.Services.AddHostedService<EventPump>();

        await builder.Build().RunAsync();
    }
}
