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
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class SignInWithRecoveryPassword : IEndpoint
{
    private const int EmailMaximumLength = 256;

    /// <summary>
    /// Signs in with a configured recovery password.
    /// </summary>
    public sealed record Request(string? Email, string? Password);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Email)
                .NotEmpty().WithMessage("The email address is required.")
                .EmailAddress().WithMessage("The email address must be valid.")
                .MaximumLength(EmailMaximumLength).WithMessage("The email address must not exceed 256 characters.");
            RuleFor(request => request.Password)
                .NotEmpty().WithMessage("The recovery password is required.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/password/sign-in", Handle)
            .WithName(nameof(SignInWithRecoveryPassword))
            .Validated()
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Ok<CurrentIdentityResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        UserManager<XpenseUser> userManager,
        SignInManager<XpenseUser> signInManager,
        IRecoveryPasswordVerifier recoveryPasswordVerifier,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email!);
        var userId = user?.Id ?? Guid.Empty;
        var wrapper = await dbContext.VaultWrappers.SingleOrDefaultAsync(item =>
            item.UserId == userId && item.Kind == VaultWrapperKind.RecoveryPassword,
            cancellationToken);
        var passwordVerified = await recoveryPasswordVerifier.VerifyAsync(
            wrapper is null ? null : user,
            request.Password!);

        if (!passwordVerified || user is null || wrapper is null)
            throw new PasskeySignInInvalidException();

        var now = DateTime.UtcNow;
        wrapper.LastUsedAt = now;
        wrapper.UpdatedAt = now;
        var wrappers = await dbContext.VaultWrappers
            .Where(item => item.UserId == user.Id)
            .ToListAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await signInManager.SignInAsync(user, false);

        return TypedResults.Ok(CurrentIdentityResponse.Of(user, wrappers, unlockableWrapperId: wrapper.Id));
    }
}
