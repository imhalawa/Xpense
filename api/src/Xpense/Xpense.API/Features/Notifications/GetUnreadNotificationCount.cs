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

public sealed class GetUnreadNotificationCount : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/notifications/unread-count", Handle)
            .WithName(nameof(GetUnreadNotificationCount));

    private static async Task<Ok<UnreadCountResponse>> Handle(XpenseDbContext dbContext, CancellationToken cancellationToken) =>
        TypedResults.Ok(new UnreadCountResponse(
            await dbContext.Notifications.CountAsync(notification => notification.ReadAt == null, cancellationToken)));
}
