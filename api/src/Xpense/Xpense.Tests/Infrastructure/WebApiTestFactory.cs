using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
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
    private IPasswordHasher<XpenseUser>? passwordHasher;
    private IDataProtectionProvider? dataProtectionProvider;
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

    public WebApiTestFactory WithPasswordHasher(IPasswordHasher<XpenseUser> value)
    {
        passwordHasher = value;
        return this;
    }

    public WebApiTestFactory WithDataProtectionProvider(IDataProtectionProvider value)
    {
        dataProtectionProvider = value;
        return this;
    }

    public async Task<HttpClient> CreateAuthenticatedClient()
    {
        using var bootstrapClient = CreateClient(new()
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = false
        });
        var userId = Guid.CreateVersion7();
        var email = $"api-suite-{userId:N}@example.test";
        string sessionCookieName;
        string antiforgeryCookieName;

        await using (var scope = Services.CreateAsyncScope())
        {
            sessionCookieName = scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme)
                .Cookie.Name
                ?? throw new InvalidOperationException("The session cookie has no name");
            antiforgeryCookieName = scope.ServiceProvider
                .GetRequiredService<IOptions<AntiforgeryOptions>>()
                .Value.Cookie.Name
                ?? throw new InvalidOperationException("The antiforgery cookie has no name");
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            dbContext.Users.Add(new XpenseUser
            {
                Id = userId,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                State = AccountState.Active,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var signInResponse = await bootstrapClient.PostAsync(
            $"/test/authentication/sign-in/{userId}",
            null);
        if (signInResponse.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidOperationException("The test session could not be created");
        var sessionCookie = Cookie(signInResponse, $"{sessionCookieName}=");

        using var antiforgeryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/antiforgery");
        antiforgeryRequest.Headers.Add("Cookie", sessionCookie);
        var antiforgeryResponse = await bootstrapClient.SendAsync(antiforgeryRequest);
        if (antiforgeryResponse.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("The antiforgery token could not be created");
        var token = await antiforgeryResponse.Content.ReadFromJsonAsync<AntiforgeryResponse>()
            ?? throw new InvalidOperationException("The antiforgery endpoint returned no token");
        var antiforgeryCookie = Cookie(antiforgeryResponse, $"{antiforgeryCookieName}=");

        var client = CreateDefaultClient(new AuthenticatedClient(
            sessionCookie,
            antiforgeryCookie,
            token.RequestToken));
        client.BaseAddress = new Uri("http://localhost");
        return client;
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
            services.AddSingleton<IStartupFilter, AntiforgeryValidationStartupFilter>();

            if (passwordHasher is not null)
            {
                services.RemoveAll<IPasswordHasher<XpenseUser>>();
                services.AddSingleton(passwordHasher);
            }

            if (dataProtectionProvider is not null)
            {
                services.RemoveAll<IDataProtectionProvider>();
                services.AddSingleton(dataProtectionProvider);
            }

            if (currentUserId.HasValue)
            {
                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(currentUserId.Value));
                services.AddSingleton<IStartupFilter>(new CurrentUserStartupFilter(currentUserId.Value));
            }
        });
    }

    private static string Cookie(HttpResponseMessage response, string prefix)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal));
        var separator = setCookie.IndexOf(';', StringComparison.Ordinal);
        return separator < 0 ? setCookie : setCookie[..separator];
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

        public bool IsAuthenticated => true;
    }

    private sealed class CurrentUserStartupFilter(Guid userId) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
        {
            application.Use(async (httpContext, nextMiddleware) =>
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"));
                await nextMiddleware();
            });
            next(application);
        };
    }

    private sealed class AntiforgeryValidationStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
        {
            next(application);
            application.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/test/authentication/antiforgery-anonymous", Validate)
                    .AllowAnonymous()
                    .WithMetadata(TestEndpointMetadata.Instance);
                endpoints.MapPost("/test/authentication/antiforgery-protected", Validate)
                    .RequireAuthorization()
                    .WithMetadata(TestEndpointMetadata.Instance);
                endpoints.MapPost("/test/authentication/sign-in/{userId:guid}", SignIn)
                    .AllowAnonymous()
                    .WithMetadata(TestEndpointMetadata.Instance);
                endpoints.MapGet("/test/errors/{kind}", ThrowError)
                    .AllowAnonymous()
                    .WithMetadata(TestEndpointMetadata.Instance);
            });
        };

        private static IResult ThrowError(string kind) => kind switch
        {
            "invitation-conflict" => throw new InvitationStateConflictException(),
            "last-wrapper" => throw new LastVaultWrapperException(),
            "grant-duplicate" => throw new ResourceGrantAlreadyActiveException(),
            "invitation-invalid" => throw new InvitationInvalidException(),
            "validation" => throw new ValidationException([new ValidationFailure("Value", "The value is invalid.")]),
            "domain" => throw new GroupOwnerCannotLeaveException(Guid.Empty),
            "persistence" => throw new AccountCreationFailedException("test"),
            "fallback" => throw new InvalidOperationException("unsafe"),
            _ => Results.NoContent()
        };

        private static async Task<IResult> SignIn(
            Guid userId,
            UserManager<XpenseUser> userManager,
            SignInManager<XpenseUser> signInManager)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());

            if (user is null)
                return Results.NotFound();

            await signInManager.SignInAsync(user, false);
            return Results.NoContent();
        }

        private static async Task<IResult> Validate(HttpContext httpContext, IAntiforgery antiforgery)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
                return Results.NoContent();
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }
        }
    }

    private sealed record AntiforgeryResponse(string RequestToken);
}

internal sealed class TestEndpointMetadata
{
    public static TestEndpointMetadata Instance { get; } = new();
}
