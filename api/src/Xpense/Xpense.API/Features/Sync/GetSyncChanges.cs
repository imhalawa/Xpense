using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class GetSyncChanges : IEndpoint
{
    private const int DefaultPageSize = 100;
    private const int MaxPageSize = 200;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/sync/changes", Handle)
            .WithName(nameof(GetSyncChanges));

    private static async Task<Ok<SyncChangesResponse>> Handle(
        SyncAuthorization authorization,
        XpenseDbContext dbContext,
        ICurrentUser currentUser,
        CancellationToken cancellationToken,
        string? cursor = null,
        int pageSize = DefaultPageSize)
    {
        var sequenceNumber = DecodeCursor(cursor);
        var effectivePageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var selected = await authorization.ReadableRecords()
            .Where(record => record.SequenceNumber > sequenceNumber)
            .OrderBy(record => record.SequenceNumber)
            .Take(effectivePageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = selected.Count > effectivePageSize;
        var records = selected.Take(effectivePageSize).ToArray();
        var envelopes = await ReadEnvelopes(records, dbContext, currentUser.Id, cancellationToken);
        var response = records
            .Select(record => EncryptedRecordResponse.Of(
                record,
                envelopes.GetValueOrDefault(record.Id, [])))
            .ToArray();
        var nextSequenceNumber = records.Length == 0 ? sequenceNumber : records[^1].SequenceNumber;

        return TypedResults.Ok(new SyncChangesResponse(
            response,
            EncodeCursor(nextSequenceNumber),
            hasMore));
    }

    private static async Task<Dictionary<Guid, RecordEnvelopeResponse[]>> ReadEnvelopes(
        EncryptedRecord[] records,
        XpenseDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (records.Length == 0)
            return [];

        var recordIds = records.Select(record => record.Id).ToArray();
        var ownedRecordIds = records
            .Where(record => record.OwnerUserId == userId)
            .Select(record => record.Id)
            .ToArray();
        var groupIds = await dbContext.GroupMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.State == MembershipState.Active)
            .Select(membership => membership.GroupId)
            .ToArrayAsync(cancellationToken);
        var envelopes = await dbContext.RecordEnvelopes
            .AsNoTracking()
            .Where(envelope =>
                recordIds.Contains(envelope.EncryptedRecordId) &&
                (envelope.GroupId.HasValue && groupIds.Contains(envelope.GroupId.Value) ||
                 !envelope.GroupId.HasValue && ownedRecordIds.Contains(envelope.EncryptedRecordId)))
            .ToArrayAsync(cancellationToken);

        return envelopes
            .GroupBy(envelope => envelope.EncryptedRecordId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(envelope => envelope.GroupId).Select(RecordEnvelopeResponse.Of).ToArray());
    }

    private static long DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return 0;

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(cursor);
            if (bytes.Length != sizeof(long))
                throw new InvalidSyncCursorException();

            var sequenceNumber = BinaryPrimitives.ReadInt64BigEndian(bytes);
            return sequenceNumber >= 0 ? sequenceNumber : throw new InvalidSyncCursorException();
        }
        catch (FormatException exception)
        {
            throw new InvalidSyncCursorException(exception);
        }
    }

    private static string EncodeCursor(long sequenceNumber)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, sequenceNumber);
        return WebEncoders.Base64UrlEncode(bytes);
    }
}
