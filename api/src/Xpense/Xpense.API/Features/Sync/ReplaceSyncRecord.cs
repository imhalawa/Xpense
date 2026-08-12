using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class ReplaceSyncRecord : IEndpoint
{
    private const int MaxPayloadLength = 65536;

    /// <summary>A replacement payload bound to the revision the client last read.</summary>
    public sealed record Request(
        long ExpectedRevision,
        byte[] Payload);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ExpectedRevision)
                .GreaterThan(0).WithMessage("The expected revision must be greater than zero.");
            RuleFor(request => request.Payload)
                .NotEmpty().WithMessage("The record payload is required.")
                .Must(payload => payload.Length <= MaxPayloadLength)
                .WithMessage("The record payload is too large.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/sync/records/{id:guid}", Handle)
            .WithName(nameof(ReplaceSyncRecord))
            .Validated();

    private static async Task<Results<
        Ok<EncryptedRecordResponse>,
        Conflict<EncryptedRecordResponse>,
        NotFound>> Handle(
        Guid id,
        Request request,
        SyncAuthorization authorization,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await authorization.CanWrite(id, cancellationToken))
            return TypedResults.NotFound();

        var updatedAt = DateTime.UtcNow;
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "Xpense"."EncryptedRecords"
            SET "Revision" = "Revision" + 1,
                "Payload" = {request.Payload},
                "SequenceNumber" = nextval('"Xpense"."EncryptedRecordSequence"'),
                "UpdatedAt" = {updatedAt}
            WHERE "Id" = {id} AND "Revision" = {request.ExpectedRevision}
            """,
            cancellationToken);
        var latest = await authorization.ReadableRecords()
            .SingleAsync(record => record.Id == id, cancellationToken);
        var response = EncryptedRecordResponse.Of(latest);

        return affected == 1
            ? TypedResults.Ok(response)
            : TypedResults.Conflict(response);
    }
}
