using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class SignInWithPasskey : IEndpoint
{
    /// <summary>
    /// Completes a passkey sign-in ceremony.
    /// </summary>
    public sealed record Request(Guid PendingPasskeyAssertionId, string? CredentialJson);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.PendingPasskeyAssertionId)
                .NotEmpty().WithMessage("The passkey assertion is required.");
            RuleFor(request => request.CredentialJson)
                .NotEmpty().WithMessage("The passkey credential is required.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/passkey/sign-in", Handle)
            .WithName(nameof(SignInWithPasskey))
            .Validated()
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Ok<CurrentIdentityResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        UserManager<XpenseUser> userManager,
        SignInManager<XpenseUser> signInManager,
        IPasskeyHandler<XpenseUser> passkeyHandler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pendingAssertion = await dbContext.PendingPasskeyAssertions
            .AsNoTracking()
            .SingleOrDefaultAsync(assertion =>
                assertion.Id == request.PendingPasskeyAssertionId &&
                assertion.ConsumedAt == null &&
                assertion.ExpiresAt > now,
                cancellationToken);

        if (pendingAssertion is null)
            return Invalid<Ok<CurrentIdentityResponse>>();

        var consumed = await dbContext.PendingPasskeyAssertions
            .Where(assertion =>
                assertion.Id == pendingAssertion.Id &&
                assertion.ConsumedAt == null &&
                assertion.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(assertion => assertion.ConsumedAt, now),
                cancellationToken);

        if (consumed != 1)
            return Invalid<Ok<CurrentIdentityResponse>>();

        var assertionResult = await passkeyHandler.PerformAssertionAsync(new PasskeyAssertionContext
        {
            HttpContext = httpContext,
            CredentialJson = request.CredentialJson!,
            AssertionState = pendingAssertion.AssertionState
        });

        if (!assertionResult.Succeeded ||
            assertionResult.User is null ||
            assertionResult.Passkey is null)
            return Invalid<Ok<CurrentIdentityResponse>>();

        var user = await userManager.FindByIdAsync(assertionResult.User.Id.ToString());
        if (user is null ||
            (pendingAssertion.NormalizedEmail is not null && pendingAssertion.NormalizedEmail != user.NormalizedEmail))
            return Invalid<Ok<CurrentIdentityResponse>>();

        var passkeyUpdated = await userManager.AddOrUpdatePasskeyAsync(user, assertionResult.Passkey);
        if (!passkeyUpdated.Succeeded)
            return Invalid<Ok<CurrentIdentityResponse>>();

        var wrappers = await dbContext.VaultWrappers
            .Where(wrapper => wrapper.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var matchingWrapper = wrappers.SingleOrDefault(wrapper =>
            wrapper.CredentialId is not null &&
            wrapper.CredentialId.SequenceEqual(assertionResult.Passkey.CredentialId));

        if (matchingWrapper is not null)
        {
            matchingWrapper.LastUsedAt = now;
            matchingWrapper.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await signInManager.SignInAsync(user, false);

        return TypedResults.Ok(CurrentIdentityResponse.Of(user, wrappers, assertionResult.Passkey.CredentialId));
    }

    private static T Invalid<T>() => throw new PasskeySignInInvalidException();
}
