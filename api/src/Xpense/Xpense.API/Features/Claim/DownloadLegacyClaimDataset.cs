using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Claim;

public sealed class DownloadLegacyClaimDataset : IEndpoint
{
    /// <summary>
    /// Identifies the one-time legacy claim token whose encrypted dataset will be downloaded.
    /// </summary>
    public sealed record Request(string? ClaimToken);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/claim/dataset", Handle)
            .WithName(nameof(DownloadLegacyClaimDataset))
            .RequireAuthorization();

    private static async Task<Ok<LegacyClaimDatasetResponse>> Handle(
        Request request,
        IOptions<LegacyClaimOptions> options,
        ICurrentUser currentUser,
        LegacyClaimTransactionLock claimTransactionLock,
        LegacyClaimDatasetBuilder datasetBuilder,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled ||
            !options.Value.TryGetDesignatedUserId(out var designatedUserId) ||
            designatedUserId != currentUser.Id ||
            !LegacyClaimTokenCodec.TryHash(request.ClaimToken, out var tokenHash))
        {
            throw new LegacyClaimInvalidException();
        }

        await using var claimLock = await claimTransactionLock.AcquireSourceSnapshot(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = DateTime.UtcNow;
        var claimToken = await dbContext.ClaimTokens.SingleOrDefaultAsync(
            stored => stored.Purpose == ClaimToken.LegacyPurpose &&
                      stored.UserId == currentUser.Id &&
                      stored.TokenHash.SequenceEqual(tokenHash),
            cancellationToken);
        if (claimToken is null || claimToken.ConsumedAt is not null || claimToken.ExpiresAt <= now)
            throw new LegacyClaimInvalidException();

        var dataset = await datasetBuilder.Build(cancellationToken);
        if (claimToken.DatasetDownloadedAt is null)
        {
            claimToken.DatasetDownloadedAt = now;
            claimToken.ExpectedRecordCount = dataset.Response.RecordCount;
            claimToken.ExpectedTypeCounts = JsonSerializer.Serialize(dataset.Response.Counts);
            claimToken.ExpectedManifestHash = dataset.ManifestHash;
            claimToken.ExpectedSourceContentHash = dataset.SourceContentHash;
            claimToken.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (claimToken.ExpectedRecordCount != dataset.Response.RecordCount ||
                 claimToken.ExpectedManifestHash is null ||
                 !claimToken.ExpectedManifestHash.SequenceEqual(dataset.ManifestHash) ||
                 claimToken.ExpectedSourceContentHash is null ||
                 !claimToken.ExpectedSourceContentHash.SequenceEqual(dataset.SourceContentHash))
        {
            throw new LegacyClaimVerificationFailedException();
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(dataset.Response);
    }
}
