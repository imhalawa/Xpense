using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;

namespace Xpense.API.Features.Notifications;

public sealed class MarkAllNotificationsRead : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/notifications/read-all", Handle)
            .WithName(nameof(MarkAllNotificationsRead));

    private static async Task<Ok<MarkAllReadResponse>> Handle(XpenseDbContext dbContext, CancellationToken cancellationToken)
    {
        var readAt = DateTime.UtcNow;

        var marked = await dbContext.Notifications
            .Where(notification => notification.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(notification => notification.ReadAt, readAt)
                    .SetProperty(notification => notification.UpdatedAt, readAt),
                cancellationToken);

        return TypedResults.Ok(new MarkAllReadResponse(marked));
    }
}

public sealed record MarkAllReadResponse(int Marked);
