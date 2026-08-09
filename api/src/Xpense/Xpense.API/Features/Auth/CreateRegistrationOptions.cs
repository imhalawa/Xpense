using System.Data;
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
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class CreateRegistrationOptions : IEndpoint
{
    private const int EmailMaximumLength = 256;
    private const int RegistrationLifetimeMinutes = 5;

    /// <summary>
    /// Starts a passkey registration ceremony for an email address.
    /// </summary>
    public sealed record Request(string Email);

    /// <summary>
    /// Provides browser passkey options and the pending registration identifier.
    /// </summary>
    public sealed record Response(string OptionsJson, Guid PendingRegistrationId);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Email)
                .NotEmpty().WithMessage("The email address is required.")
                .EmailAddress().WithMessage("The email address must be valid.")
                .MaximumLength(EmailMaximumLength).WithMessage("The email address must not exceed 256 characters.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/register/options", Handle)
            .WithName(nameof(CreateRegistrationOptions))
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
        var normalizedEmail = userManager.NormalizeEmail(request.Email);
        var userEntity = new PasskeyUserEntity
        {
            Id = Guid.CreateVersion7().ToString(),
            Name = normalizedEmail,
            DisplayName = normalizedEmail
        };
        var options = await passkeyHandler.MakeCreationOptionsAsync(userEntity, httpContext);

        var now = DateTime.UtcNow;
        var pendingRegistration = new PendingRegistration
        {
            Id = Guid.CreateVersion7(),
            NormalizedEmail = normalizedEmail,
            AttestationState = options.AttestationState ?? string.Empty,
            ExpiresAt = now.AddMinutes(RegistrationLifetimeMinutes),
            CreatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({normalizedEmail}, 0))",
            cancellationToken);
        await dbContext.PendingRegistrations
            .Where(registration => registration.NormalizedEmail == normalizedEmail && registration.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(registration => registration.ConsumedAt, now),
                cancellationToken);
        dbContext.PendingRegistrations.Add(pendingRegistration);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new Response(options.CreationOptionsJson, pendingRegistration.Id));
    }
}
