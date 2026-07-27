using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Tags;

public sealed class DeleteTag : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/tags/{id:int}", Handle).WithName(nameof(DeleteTag));

    private static async Task<NoContent> Handle(int id, XpenseDbContext db, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)
                  ?? throw new TagNotFoundException(id);

        // Soft delete: the global query filter hides IsDeleted rows. Preserved from the
        // repository's Delete, which did exactly this rather than removing the row.
        tag.MarkAsDeleted();
        tag.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new TagDeletionFailedException(id);

        return TypedResults.NoContent();
    }
}
