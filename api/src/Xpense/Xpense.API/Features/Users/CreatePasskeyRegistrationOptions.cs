using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Persistence;

namespace Xpense.API.Features.Users;

public sealed class CreatePasskeyRegistrationOptions : IEndpoint
{
    private const int RegistrationLifetimeMinutes = 5;

    /// <summary>
    /// Provides browser options and the authoritative pending passkey registration identifier.
    /// </summary>
    public sealed record Response(string OptionsJson, Guid PendingRegistrationId);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/me/passkeys/options", Handle)
            .WithName(nameof(CreatePasskeyRegistrationOptions))
            .RequireAuthorization();

    private static async Task<Results<Ok<Response>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        IPasskeyHandler<XpenseUser> passkeyHandler,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        var userEntity = new PasskeyUserEntity
        {
            Id = user.Id.ToString(),
            Name = user.NormalizedEmail!,
            DisplayName = user.NormalizedEmail!
        };
        var options = await passkeyHandler.MakeCreationOptionsAsync(userEntity, httpContext);
        var now = DateTime.UtcNow;
        var pendingRegistration = new PendingRegistration
        {
            Id = Guid.CreateVersion7(),
            NormalizedEmail = user.NormalizedEmail!,
            AttestationState = options.AttestationState ?? string.Empty,
            ExpiresAt = now.AddMinutes(RegistrationLifetimeMinutes),
            CreatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({user.NormalizedEmail!}, 0))",
            cancellationToken);
        await dbContext.PendingRegistrations
            .Where(registration =>
                registration.NormalizedEmail == user.NormalizedEmail &&
                registration.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(registration => registration.ConsumedAt, now),
                cancellationToken);
        dbContext.PendingRegistrations.Add(pendingRegistration);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new Response(options.CreationOptionsJson, pendingRegistration.Id));
    }
}
