using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Persistence;

namespace Xpense.Tests.Infrastructure;

public sealed class WebApiTestFactory : WebApplicationFactory<Program>
{
    private static readonly IReadOnlyDictionary<string, string?> TestConfiguration =
        new Dictionary<string, string?>
        {
            ["Authentication:RelyingPartyDomain"] = "identity.example.test",
            ["Authentication:RelyingPartyName"] = "Xpense Test",
            ["Authentication:AllowedOrigins:0"] = "https://app.example.test",
            ["Authentication:Registration"] = "Open",
            ["Authentication:PublicUrl"] = "https://app.example.test",
            ["DataProtection:KeyDirectory"] = "/tmp/xpense-tests/keys",
            ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1"
        };

    private readonly string connectionString;
    private readonly IInterceptor[] interceptors;
    private Guid? currentUserId;
    private RegistrationPolicy? registrationPolicy;

    public WebApiTestFactory(string connectionString, params IInterceptor[] interceptors)
    {
        this.connectionString = connectionString;
        this.interceptors = interceptors;
    }

    public WebApiTestFactory AsUser(Guid userId)
    {
        currentUserId = userId;
        return this;
    }

    public WebApiTestFactory WithRegistrationPolicy(RegistrationPolicy policy)
    {
        registrationPolicy = policy;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var setting in TestConfiguration)
            builder.UseSetting(setting.Key, setting.Value!);

        if (registrationPolicy.HasValue)
            builder.UseSetting("Authentication:Registration", registrationPolicy.Value.ToString());

        builder.ConfigureServices(services =>
        {
            RemoveProductionDbContext(services);
            services.AddDbContext<XpenseDbContext>(options =>
                options.UseNpgsql(connectionString).AddInterceptors(interceptors));
            services.RemoveAll<IPasskeyHandler<XpenseUser>>();
            services.AddScoped<IPasskeyHandler<XpenseUser>, SoftwareAuthenticator>();

            if (currentUserId.HasValue)
            {
                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(currentUserId.Value));
            }
        });
    }

    private static void RemoveProductionDbContext(IServiceCollection services)
    {
        var doomed = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(XpenseDbContext) ||
                descriptor.ServiceType == typeof(DbContextOptions) ||
                descriptor.ServiceType == typeof(DbContextOptions<XpenseDbContext>) ||
                (descriptor.ServiceType.IsGenericType &&
                 descriptor.ServiceType.GetGenericArguments().Contains(typeof(XpenseDbContext))))
            .ToList();

        foreach (var descriptor in doomed)
            services.Remove(descriptor);
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id { get; } = id;
    }
}
