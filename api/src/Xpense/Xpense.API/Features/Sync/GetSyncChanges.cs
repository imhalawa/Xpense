using System;
using System.Buffers.Binary;
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
using Xpense.Domain.Exceptions;

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
        var response = records.Select(EncryptedRecordResponse.Of).ToArray();
        var nextSequenceNumber = records.Length == 0 ? sequenceNumber : records[^1].SequenceNumber;

        return TypedResults.Ok(new SyncChangesResponse(
            response,
            EncodeCursor(nextSequenceNumber),
            hasMore));
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
