using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class RevokeResourceGrant : IEndpoint
{
    private const string RevocationWarning = "Revocation cannot erase data that a former member already saved.";

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/groups/{groupId:guid}/grants/{grantId:guid}", Handle)
            .WithName(nameof(RevokeResourceGrant))
            .RequireAuthorization();

    private static async Task<Results<Ok<KeyRotationResponse>, NotFound>> Handle(
        Guid groupId,
        Guid grantId,
        ResourceTransactionLock resourceTransactionLock,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resourceId = await dbContext.ResourceGrants.AsNoTracking()
            .Where(grant => grant.Id == grantId && grant.GroupId == groupId)
            .Select(grant => (Guid?)grant.ResourceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!resourceId.HasValue)
            return TypedResults.NotFound();

        await using var resourceLock = await resourceTransactionLock.Acquire(resourceId.Value, cancellationToken);
        await using var groupLock = await groupTransactionLock.Acquire(groupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var userId = currentUser.Id;
        var grant = await dbContext.ResourceGrants.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == grantId &&
                candidate.GroupId == groupId &&
                candidate.ResourceId == resourceId.Value &&
                candidate.State == GrantState.Active &&
                dbContext.Groups.Any(groupEntity =>
                    groupEntity.Id == candidate.GroupId &&
                    !groupEntity.IsDeleted) &&
                dbContext.SharedResources.Any(resource =>
                    resource.Id == candidate.ResourceId &&
                    resource.Type == candidate.ResourceType &&
                    resource.OwnerUserId == userId),
            cancellationToken);
        if (grant is null)
            return TypedResults.NotFound();

        grant.Revoke(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new KeyRotationResponse(true, RevocationWarning));
    }
}
