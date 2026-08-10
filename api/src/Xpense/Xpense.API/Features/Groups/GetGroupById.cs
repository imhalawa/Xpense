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
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class GetGroupById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/groups/{groupId:guid}", Handle)
            .WithName(nameof(GetGroupById))
            .RequireAuthorization();

    private static async Task<Results<Ok<GroupResponse>, NotFound>> Handle(
        Guid groupId,
        AccessRules accessRules,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await accessRules.IsActiveMember(groupId, cancellationToken))
            return TypedResults.NotFound();

        var userId = currentUser.Id;
        var item = await (
            from membership in dbContext.GroupMemberships.AsNoTracking()
            join groupEntity in dbContext.Groups.AsNoTracking()
                on membership.GroupId equals groupEntity.Id
            where groupEntity.Id == groupId &&
                  membership.UserId == userId &&
                  membership.State == MembershipState.Active &&
                  !groupEntity.IsDeleted
            select new { Group = groupEntity, Membership = membership })
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(GroupResponse.Of(item.Group, item.Membership));
    }
}
