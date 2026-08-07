using System.Linq;
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

public sealed class ListNotifications : IEndpoint
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;

    private const int MaxPageSize = 100;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/notifications", Handle).WithName(nameof(ListNotifications));

    private static async Task<Ok<NotificationPageResponse>> Handle(
        XpenseDbContext dbContext,
        CancellationToken cancellationToken,
        int page = DefaultPage,
        int pageSize = DefaultPageSize,
        bool unread = false)
    {
        if (page <= 0 || pageSize <= 0 || pageSize > MaxPageSize)
            throw new InvalidFilteredResultParams(page, pageSize);

        var all = dbContext.Notifications.AsNoTracking();
        var selected = unread ? all.Where(notification => notification.ReadAt == null) : all;

        var totalItems = await selected.CountAsync(cancellationToken);
        var totalPages = totalItems / pageSize + (totalItems % pageSize > 0 ? 1 : 0);
        var unreadItems = await all.CountAsync(notification => notification.ReadAt == null, cancellationToken);

        var notifications = await selected
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new NotificationPageResponse(
            notifications.Select(NotificationResponse.Of).ToArray(),
            page,
            pageSize,
            totalItems,
            totalPages,
            unreadItems));
    }
}
