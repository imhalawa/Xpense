using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class DeleteSyncRecord : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/sync/records/{id:guid}", Handle)
            .WithName(nameof(DeleteSyncRecord));

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid id,
        SyncAuthorization authorization,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await authorization.CanWrite(id, cancellationToken))
            return TypedResults.NotFound();

        var record = await dbContext.EncryptedRecords
            .SingleAsync(item => item.Id == id, cancellationToken);

        if (!record.IsDeleted)
        {
            record.MarkAsDeleted();
            record.Touch();
            record.SequenceNumber = await dbContext.Database
                .SqlQueryRaw<long>("SELECT nextval('\"Xpense\".\"EncryptedRecordSequence\"') AS \"Value\"")
                .SingleAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
