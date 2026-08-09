using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xpense.API.Extensions;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class AuthenticationConfigurationTests
{
    private static readonly IReadOnlyDictionary<string, string?> Settings =
        new Dictionary<string, string?>
        {
            ["Authentication:RelyingPartyDomain"] = "identity.example.test",
            ["Authentication:RelyingPartyName"] = "Xpense Test",
            ["Authentication:AllowedOrigins:0"] = "https://app.example.test",
            ["Authentication:Registration"] = "Open",
            ["Authentication:PublicUrl"] = "https://app.example.test",
            ["DataProtection:KeyDirectory"] = "/tmp/xpense-authentication-tests",
            ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1"
        };

    [Test]
    public async Task An_unauthenticated_request_to_a_protected_route_returns_401_not_a_redirect()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/test/authentication/protected");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Test]
    public async Task An_authenticated_request_without_permission_returns_403()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var signInResponse = await client.GetAsync("/test/authentication/sign-in");
        var response = await client.GetAsync("/test/authentication/forbidden");

        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Location.Should().BeNull();
    }

    [Test]
    public async Task Cors_allows_configured_origins_with_credentials()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/test/authentication/protected");
        request.Headers.Add("Origin", "https://app.example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle("https://app.example.test");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle("true");
    }

    [Test]
    public async Task Forwarded_client_addresses_partition_the_auth_rate_limit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var permit = 0; permit < 10; permit++)
            (await SendRateLimitedRequest(client, "203.0.113.10")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendRateLimitedRequest(client, "203.0.113.10")).StatusCode
            .Should().Be(HttpStatusCode.TooManyRequests);
        (await SendRateLimitedRequest(client, "203.0.113.11")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public void The_shared_testing_factory_starts_with_startup_validation_enabled()
    {
        using var factory = new WebApiTestFactory("Host=unused;Database=unused;Username=unused;Password=unused");

        Action act = () => factory.CreateClient().Dispose();

        act.Should().NotThrow();
    }

    [Test]
    public void Recovery_password_services_resolve_with_development_scope_validation()
    {
        var settings = new Dictionary<string, string?>(Settings)
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused;Username=unused;Password=unused"
        };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.ConfigurePersistence(builder.Configuration);
        builder.Services.AddXpenseAuthentication(builder.Configuration);
        builder.Services.AddDomainServices();

        Action act = () =>
        {
            using var application = builder.Build();
            using var scope = application.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IRecoveryPasswordVerifier>();
        };

        act.Should().NotThrow();
    }

    [Test]
    public void The_session_cookie_is_http_only_secure_and_same_site_lax()
    {
        using var serviceProvider = CreateServiceProvider(Environments.Staging);
        var cookie = serviceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme)
            .Cookie;

        cookie.Name.Should().Be("xpense.session");
        cookie.HttpOnly.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Lax);
        cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
    }

    [Test]
    public async Task The_relying_party_domain_comes_from_configuration_not_the_host_header()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Settings)
            .Build();
        using var serviceProvider = CreateServiceProvider(configuration, Environments.Staging);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("untrusted.example.test");

        var passkeys = serviceProvider
            .GetRequiredService<IOptions<IdentityPasskeyOptions>>()
            .Value;
        var isAllowed = await passkeys.ValidateOrigin!(new PasskeyOriginValidationContext
        {
            HttpContext = httpContext,
            Origin = "https://app.example.test",
            CrossOrigin = false
        });
        var isRejected = await passkeys.ValidateOrigin!(new PasskeyOriginValidationContext
        {
            HttpContext = httpContext,
            Origin = "https://untrusted.example.test",
            CrossOrigin = false
        });

        passkeys.ServerDomain.Should().Be("identity.example.test");
        isAllowed.Should().BeTrue();
        isRejected.Should().BeFalse();
    }

    [Test]
    public async Task Starting_outside_development_with_a_localhost_relying_party_domain_fails_fast()
    {
        var settings = new Dictionary<string, string?>(Settings)
        {
            ["Authentication:RelyingPartyDomain"] = "localhost",
            ["Authentication:PublicUrl"] = "http://localhost:5173"
        };
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Staging
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddXpenseAuthentication(builder.Configuration);
        using var host = builder.Build();

        Func<Task> act = () => host.StartAsync();

        await act.Should()
            .ThrowAsync<OptionsValidationException>()
            .WithMessage("*relying-party domain cannot be localhost outside development*");
    }

    private static AuthenticationTestFactory CreateFactory(
        string environment = "Testing") => new(environment);

    private static Task<HttpResponseMessage> SendRateLimitedRequest(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/test/authentication/rate-limited");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return client.SendAsync(request);
    }

    private static ServiceProvider CreateServiceProvider(string environment) =>
        CreateServiceProvider(
            new ConfigurationBuilder().AddInMemoryCollection(Settings).Build(),
            environment);

    private static ServiceProvider CreateServiceProvider(IConfiguration configuration, string environment)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment));
        services.AddXpenseAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class AuthenticationTestFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            foreach (var setting in Settings)
                builder.UseSetting(setting.Key, setting.Value!);

            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ProtectedEndpointStartupFilter>());
        }
    }

    private sealed class ProtectedEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
        {
            application.Use((httpContext, nextMiddleware) =>
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
                return nextMiddleware(httpContext);
            });
            next(application);
            application.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/authentication/protected", () => Results.Ok())
                    .RequireAuthorization();
                endpoints.MapGet("/test/authentication/forbidden", () => Results.Ok())
                    .RequireAuthorization(policy => policy.RequireClaim("permission", "granted"));
                endpoints.MapGet("/test/authentication/sign-in", SignIn);
                endpoints.MapGet("/test/authentication/rate-limited", () => Results.Ok())
                    .RequireRateLimiting("auth");
            });
        };

        private static Task SignIn(HttpContext httpContext)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString())],
                IdentityConstants.ApplicationScheme);
            return httpContext.SignInAsync(
                IdentityConstants.ApplicationScheme,
                new ClaimsPrincipal(identity));
        }
    }

    private sealed class TestHostEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;

        public string ApplicationName { get; set; } = "Xpense.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
