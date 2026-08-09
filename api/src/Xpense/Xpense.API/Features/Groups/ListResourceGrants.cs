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

public sealed class ListResourceGrants : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/groups/{groupId:guid}/grants", Handle)
            .WithName(nameof(ListResourceGrants))
            .RequireAuthorization();

    private static async Task<Results<Ok<ResourceGrantResponse[]>, NotFound>> Handle(
        Guid groupId,
        AccessRules accessRules,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await accessRules.IsActiveMember(groupId, cancellationToken))
            return TypedResults.NotFound();

        var userId = currentUser.Id;
        var grants = await dbContext.ResourceGrants.AsNoTracking()
            .Where(grant =>
                grant.GroupId == groupId &&
                grant.State == GrantState.Active &&
                dbContext.Groups.Any(groupEntity =>
                    groupEntity.Id == grant.GroupId &&
                    !groupEntity.IsDeleted) &&
                dbContext.SharedResources.Any(resource =>
                    resource.Id == grant.ResourceId &&
                    resource.Type == grant.ResourceType) &&
                dbContext.GroupMemberships.Any(membership =>
                    membership.GroupId == grant.GroupId &&
                    membership.UserId == userId &&
                    membership.State == MembershipState.Active))
            .OrderBy(grant => grant.CreatedAt)
            .ThenBy(grant => grant.Id)
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(grants.Select(ResourceGrantResponse.Of).ToArray());
    }
}
