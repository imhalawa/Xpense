using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

public sealed class SignInWithRecoveryFile : IEndpoint
{
    private const int AuthenticationTokenMaximumLength = 512;

    /// <summary>
    /// Signs in with a single-use recovery-file authentication token.
    /// </summary>
    public sealed record Request(string? AuthenticationToken);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AuthenticationToken)
                .NotEmpty().WithMessage("The recovery authentication token is required.")
                .MaximumLength(AuthenticationTokenMaximumLength).WithMessage("The recovery authentication token must not exceed 512 characters.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/recovery/sign-in", Handle)
            .WithName(nameof(SignInWithRecoveryFile))
            .Validated()
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Ok<CurrentIdentityResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        SignInManager<XpenseUser> signInManager,
        CancellationToken cancellationToken)
    {
        var authenticationTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.AuthenticationToken!));
        var wrapper = await dbContext.VaultWrappers
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.AuthenticationTokenHash != null &&
                item.AuthenticationTokenHash.SequenceEqual(authenticationTokenHash) &&
                item.ConsumedAt == null,
                cancellationToken);

        if (wrapper is null)
            throw new PasskeySignInInvalidException();

        var now = DateTime.UtcNow;
        var consumed = await dbContext.VaultWrappers
            .Where(item => item.Id == wrapper.Id && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.ConsumedAt, now)
                    .SetProperty(item => item.LastUsedAt, now)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (consumed != 1)
            throw new PasskeySignInInvalidException();

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == wrapper.UserId, cancellationToken);
        if (user is null)
            throw new PasskeySignInInvalidException();

        var wrappers = await dbContext.VaultWrappers
            .Where(item => item.UserId == user.Id)
            .ToListAsync(cancellationToken);
        await signInManager.SignInAsync(user, false);

        return TypedResults.Ok(CurrentIdentityResponse.Of(user, wrappers, unlockableWrapperId: wrapper.Id));
    }
}
