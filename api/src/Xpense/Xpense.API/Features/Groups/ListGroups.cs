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
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class ListGroups : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/groups", Handle)
            .WithName(nameof(ListGroups))
            .RequireAuthorization();

    private static async Task<Ok<GroupResponse[]>> Handle(
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var memberships = await (
            from membership in dbContext.GroupMemberships.AsNoTracking()
            join groupEntity in dbContext.Groups.AsNoTracking()
                on membership.GroupId equals groupEntity.Id
            where membership.UserId == userId &&
                  membership.State == MembershipState.Active &&
                  !groupEntity.IsDeleted
            orderby groupEntity.CreatedAt, groupEntity.Id
            select new { Group = groupEntity, Membership = membership })
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(memberships
            .Select(item => GroupResponse.Of(item.Group, item.Membership))
            .ToArray());
    }
}
