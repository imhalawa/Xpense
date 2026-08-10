using System;
using System.Collections.Generic;
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
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Claim;

public sealed class CompleteLegacyClaim : IEndpoint
{
    private static readonly EncryptedRecordType[] ClaimedRecordTypes =
    [
        EncryptedRecordType.Account,
        EncryptedRecordType.Transaction,
        EncryptedRecordType.Transfer,
        EncryptedRecordType.Category,
        EncryptedRecordType.Merchant,
        EncryptedRecordType.Tag,
        EncryptedRecordType.Budget,
        EncryptedRecordType.Notification,
        EncryptedRecordType.NecessityScale
    ];

    /// <summary>
    /// Identifies the pinned legacy dataset whose encrypted replacement will be verified.
    /// </summary>
    public sealed record Request(string? ClaimToken);

    /// <summary>
    /// Confirms the exact encrypted record set that replaced the pinned legacy dataset.
    /// </summary>
    public sealed record Response(
        int RecordCount,
        IReadOnlyDictionary<string, int> Counts,
        string ManifestHash,
        DateTime CompletedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/claim/complete", Handle)
            .WithName(nameof(CompleteLegacyClaim))
            .RequireAuthorization();

    private static async Task<Ok<Response>> Handle(
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

        await using var claimLock = await claimTransactionLock.AcquireCompletionSnapshot(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = DateTime.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10));
        var claimToken = await dbContext.ClaimTokens.SingleOrDefaultAsync(
            stored => stored.Purpose == ClaimToken.LegacyPurpose &&
                      stored.UserId == currentUser.Id &&
                      stored.TokenHash.SequenceEqual(tokenHash),
            cancellationToken);
        if (claimToken is null || claimToken.ConsumedAt is not null || claimToken.ExpiresAt <= now)
            throw new LegacyClaimInvalidException();
        if (claimToken.DatasetDownloadedAt is null ||
            claimToken.ExpectedRecordCount is null ||
            claimToken.ExpectedTypeCounts is null ||
            claimToken.ExpectedManifestHash is null ||
            claimToken.ExpectedSourceContentHash is null)
        {
            throw new LegacyClaimVerificationFailedException();
        }

        var dataset = await datasetBuilder.Build(cancellationToken);
        if (claimToken.ExpectedRecordCount != dataset.Response.RecordCount ||
            !claimToken.ExpectedManifestHash.SequenceEqual(dataset.ManifestHash) ||
            !claimToken.ExpectedSourceContentHash.SequenceEqual(dataset.SourceContentHash))
        {
            throw new LegacyClaimVerificationFailedException();
        }

        var expected = dataset.ExpectedEncryptedRecords()
            .ToDictionary(record => record.Id);
        var actual = await dbContext.EncryptedRecords.AsNoTracking()
            .Where(record => record.OwnerUserId == currentUser.Id &&
                             ClaimedRecordTypes.Contains(record.RecordType))
            .ToArrayAsync(cancellationToken);
        if (actual.Length != expected.Count)
            throw new LegacyClaimVerificationFailedException();

        var expectedRoots = expected.Values
            .Where(record => record.RecordType is EncryptedRecordType.Account or EncryptedRecordType.Budget)
            .ToDictionary(
                record => record.Id,
                record => record.RecordType == EncryptedRecordType.Account
                    ? SharedResourceType.Account
                    : SharedResourceType.Budget);
        var roots = await dbContext.SharedResources.AsNoTracking()
            .Where(resource => expectedRoots.Keys.Contains(resource.Id))
            .ToArrayAsync(cancellationToken);
        if (roots.Length != expectedRoots.Count || roots.Any(resource =>
                resource.OwnerUserId != currentUser.Id ||
                !expectedRoots.TryGetValue(resource.Id, out var expectedType) ||
                resource.Type != expectedType))
        {
            throw new LegacyClaimVerificationFailedException();
        }

        var actualEnvelopes = await dbContext.RecordEnvelopes.AsNoTracking()
            .Where(envelope => envelope.GroupId == null &&
                               actual.Select(record => record.Id).Contains(envelope.EncryptedRecordId))
            .ToArrayAsync(cancellationToken);
        var personalEnvelopes = actualEnvelopes.ToDictionary(envelope => envelope.EncryptedRecordId);
        foreach (var actualRecord in actual)
        {
            if (!expected.TryGetValue(actualRecord.Id, out var expectedRecord) ||
                actualRecord.RecordType != expectedRecord.RecordType ||
                actualRecord.ProtocolVersion != LegacyClaimDatasetBuilder.ProtocolVersion ||
                actualRecord.Nonce.Length == 0 ||
                actualRecord.Ciphertext.Length == 0 ||
                actualRecord.IsDeleted != expectedRecord.IsDeleted ||
                actualRecord.ParentResourceId != expectedRecord.ParentResourceId ||
                !personalEnvelopes.TryGetValue(actualRecord.Id, out var personalEnvelope) ||
                personalEnvelope.ProtocolVersion != LegacyClaimDatasetBuilder.ProtocolVersion ||
                personalEnvelope.WrappedKey.Length == 0 ||
                personalEnvelope.Nonce.Length == 0)
            {
                throw new LegacyClaimVerificationFailedException();
            }
        }

        var counts = JsonSerializer.Deserialize<Dictionary<string, int>>(claimToken.ExpectedTypeCounts)
            ?? throw new LegacyClaimVerificationFailedException();
        if (counts.Count != dataset.Response.Counts.Count ||
            counts.Any(count => !dataset.Response.Counts.TryGetValue(count.Key, out var value) || value != count.Value))
        {
            throw new LegacyClaimVerificationFailedException();
        }

        claimToken.ConsumedAt = now;
        claimToken.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(new Response(
            claimToken.ExpectedRecordCount.Value,
            counts,
            Convert.ToHexStringLower(claimToken.ExpectedManifestHash),
            now));
    }
}
