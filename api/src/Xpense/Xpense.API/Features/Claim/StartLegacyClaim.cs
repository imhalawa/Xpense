using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Claim;

public sealed class StartLegacyClaim : IEndpoint
{
    /// <summary>
    /// Starts or restarts the one-time encrypted claim of this installation's legacy financial data.
    /// </summary>
    public sealed record Response(string ClaimToken, DateTime ExpiresAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/claim/start", Handle)
            .WithName(nameof(StartLegacyClaim))
            .RequireAuthorization();

    private static async Task<Ok<Response>> Handle(
        IOptions<LegacyClaimOptions> options,
        ICurrentUser currentUser,
        LegacyClaimTransactionLock claimTransactionLock,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled ||
            !options.Value.TryGetDesignatedUserId(out var designatedUserId) ||
            designatedUserId != currentUser.Id)
        {
            throw new LegacyClaimInvalidException();
        }

        await using var claimLock = await claimTransactionLock.Acquire(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await dbContext.ClaimTokens.SingleOrDefaultAsync(
            claimToken => claimToken.Purpose == ClaimToken.LegacyPurpose,
            cancellationToken);
        if (existing?.ConsumedAt is not null)
            throw new LegacyClaimAlreadyCompletedException();

        var now = DateTime.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10));
        var generated = LegacyClaimTokenCodec.Generate();
        if (existing is null)
        {
            existing = new ClaimToken
            {
                Id = Guid.CreateVersion7(),
                UserId = currentUser.Id,
                TokenHash = generated.Hash,
                ExpiresAt = now.AddMinutes(30),
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.ClaimTokens.Add(existing);
        }
        else
        {
            existing.UserId = currentUser.Id;
            existing.Rotate(generated.Hash, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(new Response(generated.Token, existing.ExpiresAt));
    }
}
