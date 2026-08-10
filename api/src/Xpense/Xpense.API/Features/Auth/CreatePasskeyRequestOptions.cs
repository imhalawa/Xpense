using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class CreatePasskeyRequestOptions : IEndpoint
{
    private const int EmailMaximumLength = 256;
    private const int AssertionLifetimeMinutes = 5;

    /// <summary>
    /// Starts a passkey sign-in ceremony, optionally for an email address.
    /// </summary>
    public sealed record Request(string? Email);

    /// <summary>
    /// Provides browser passkey options and the assertion identifier.
    /// </summary>
    public sealed record Response(string OptionsJson, Guid PendingPasskeyAssertionId);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            When(request => !string.IsNullOrWhiteSpace(request.Email), () =>
            {
                RuleFor(request => request.Email)
                    .EmailAddress().WithMessage("The email address must be valid.")
                    .MaximumLength(EmailMaximumLength).WithMessage("The email address must not exceed 256 characters.");
            });
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/passkey/options", Handle)
            .WithName(nameof(CreatePasskeyRequestOptions))
            .Validated()
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Ok<Response>> Handle(
        Request request,
        UserManager<XpenseUser> userManager,
        IPasskeyHandler<XpenseUser> passkeyHandler,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : userManager.NormalizeEmail(request.Email);
        var options = await passkeyHandler.MakeRequestOptionsAsync(null, httpContext);
        var now = DateTime.UtcNow;
        var pendingAssertion = new PendingPasskeyAssertion
        {
            Id = Guid.CreateVersion7(),
            NormalizedEmail = normalizedEmail,
            AssertionState = options.AssertionState ?? string.Empty,
            ExpiresAt = now.AddMinutes(AssertionLifetimeMinutes),
            CreatedAt = now
        };

        dbContext.PendingPasskeyAssertions.Add(pendingAssertion);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new Response(options.RequestOptionsJson, pendingAssertion.Id));
    }
}
