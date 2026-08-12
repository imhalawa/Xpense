using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Notifications;

public sealed class MarkNotificationRead : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/v1/notifications/{id:int}/read", Handle)
            .WithName(nameof(MarkNotificationRead));

    private static async Task<Ok<NotificationResponse>> Handle(
        int id,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                           ?? throw new NotificationNotFoundException(id);

        notification.MarkAsRead();

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(NotificationResponse.Of(notification));
    }
}
