using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using System;
using System.IO;
using System.Reflection;
using Xpense.API.ExceptionHandlers;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Domain.Events;
using Xpense.Persistence;

namespace Xpense.API.Extensions;

public static class IoC
{
    public static void ConfigurePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<XpenseDbContext>(optionsBuilder =>
        {
            optionsBuilder.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly("Xpense.Persistence"));
        });
    }

    public static void AddDomainServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<SyncAuthorization>();
        services.AddScoped(typeof(OptionResolver<>));

        services.AddScoped<IEventBus, EventBus>();
    }

    public static void AddXpenseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authentication = new XpenseAuthenticationOptions();
        configuration.GetSection(XpenseAuthenticationOptions.SectionName).Bind(authentication);

        services.AddOptions<XpenseAuthenticationOptions>()
            .Bind(configuration.GetSection(XpenseAuthenticationOptions.SectionName))
            .Validate<IHostEnvironment>(
                (options, environment) => environment.IsDevelopment() ||
                    !string.Equals(options.RelyingPartyDomain, "localhost", StringComparison.OrdinalIgnoreCase),
                "The relying-party domain cannot be localhost outside development.")
            .Validate<IHostEnvironment>(
                (options, environment) => environment.IsDevelopment() ||
                    Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var publicUrl) &&
                    publicUrl.Scheme == Uri.UriSchemeHttps,
                "The public URL must use HTTPS outside development.")
            .ValidateOnStart();

        services.AddIdentityCore<XpenseUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 14;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<XpenseDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.AddSingleton(RecoveryPasswordTimingHash.Create());
        services.AddScoped<IRecoveryPasswordVerifier, RecoveryPasswordVerifier>();

        services.Configure<IdentityPasskeyOptions>(options =>
        {
            options.ServerDomain = authentication.RelyingPartyDomain;
            options.UserVerificationRequirement = "required";
            options.ValidateOrigin = context => ValueTask.FromResult(
                Array.Exists(authentication.AllowedOrigins, origin => origin == context.Origin));
        });

        services.AddCors(options => options.AddPolicy(
            AuthenticationPolicyNames.Cors,
            policy => policy
                .WithOrigins(authentication.AllowedOrigins)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod()));

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.AddAuthorization();

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IHostEnvironment>((options, environment) =>
            {
                options.Cookie.Name = "xpense.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAntiforgery(options => options.HeaderName = "X-Xpense-Antiforgery");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthenticationPolicyNames.RateLimit, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        var keyDirectory = configuration["DataProtection:KeyDirectory"]
            ?? throw new InvalidOperationException("The data-protection key directory is required.");

        services.AddDataProtection()
            .SetApplicationName("Xpense")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
    }

    public static void AddXpenseForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var forwardedHeaders = new XpenseForwardedHeadersOptions();
        configuration.GetSection(XpenseForwardedHeadersOptions.SectionName).Bind(forwardedHeaders);

        services.AddOptions<XpenseForwardedHeadersOptions>()
            .Bind(configuration.GetSection(XpenseForwardedHeadersOptions.SectionName))
            .Validate(options => options.ForwardLimit > 0, "The forwarded-header limit must be positive.")
            .Validate(
                options => options.KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _)),
                "Every known proxy must be a valid IP address.")
            .Validate(
                options => options.KnownIPNetworks.All(network => System.Net.IPNetwork.TryParse(network, out _)),
                "Every known proxy network must be a valid CIDR range.")
            .ValidateOnStart();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = forwardedHeaders.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in forwardedHeaders.KnownProxies)
                options.KnownProxies.Add(IPAddress.Parse(proxy));

            foreach (var network in forwardedHeaders.KnownIPNetworks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
    }

    public static void AddRequestValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(IEndpoint).Assembly);
    }

    public static void AddExceptionHandlers(this IServiceCollection services)
    {
        services.AddProblemDetails();

        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<InsufficientFundsExceptionHandler>();
        services.AddExceptionHandler<NotFoundExceptionHandler>();
        services.AddExceptionHandler<AuthenticationExceptionHandler>();
        services.AddExceptionHandler<DomainRuleViolationExceptionHandler>();
        services.AddExceptionHandler<PersistenceFailedExceptionHandler>();
        services.AddExceptionHandler<FallbackExceptionHandler>();
    }

    public static void AddHealthProbe(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<XpenseDbContext>();
    }

    public static void ConfigureSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(SchemaId);
            options.TagActionsBy(description => new[] { SwaggerTags.ForRoute(description.RelativePath) });
            options.OrderActionsBy(description => SwaggerTags.ForRoute(description.RelativePath));
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Xpense.API",
                    Description = "Financial Tracker and advisor",
                    Contact = new OpenApiContact
                    {
                        Name = "Mohamed Halawa",
                        Email = "imhalawa@outlook.com",
                        Url = new Uri("https://www.halawa.dev")
                    },
                });

            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
    }

    private static string SchemaId(Type type)
    {
        var names = new List<string>();

        for (var current = type; current is not null; current = current.DeclaringType)
            names.Insert(0, current.Name);

        return string.Concat(names).Replace("`", string.Empty);
    }
}
