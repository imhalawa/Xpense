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

public sealed class ListGroupMembers : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/groups/{groupId:guid}/members", Handle)
            .WithName(nameof(ListGroupMembers))
            .RequireAuthorization();

    private static async Task<Results<Ok<GroupMemberResponse[]>, NotFound>> Handle(
        Guid groupId,
        AccessRules accessRules,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await accessRules.IsActiveMember(groupId, cancellationToken))
            return TypedResults.NotFound();

        var currentUserId = currentUser.Id;
        var members = await (
            from membership in dbContext.GroupMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.GroupId == groupId &&
                  membership.State == MembershipState.Active &&
                  dbContext.Groups.Any(groupEntity =>
                      groupEntity.Id == groupId && !groupEntity.IsDeleted) &&
                  dbContext.GroupMemberships.Any(callerMembership =>
                      callerMembership.GroupId == groupId &&
                      callerMembership.UserId == currentUserId &&
                      callerMembership.State == MembershipState.Active)
            orderby membership.Role, membership.CreatedAt, membership.UserId
            select new GroupMemberResponse(
                membership.UserId,
                user.Email!,
                membership.Role,
                membership.GroupKeyEnvelope != null))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(members);
    }
}
