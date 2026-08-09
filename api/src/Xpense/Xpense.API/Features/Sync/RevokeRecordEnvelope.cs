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
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class RevokeRecordEnvelope : IEndpoint
{
    private const string RevocationWarning = "Revocation cannot erase data that a former member already saved.";

    /// <summary>The key rotation warning after revoking a group envelope.</summary>
    public sealed record Response(bool KeyRotationRequired, string Warning);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/sync/records/{id:guid}/envelopes/{groupId:guid}", Handle)
            .WithName(nameof(RevokeRecordEnvelope));

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        Guid id,
        Guid groupId,
        ResourceTransactionLock resourceTransactionLock,
        GroupTransactionLock groupTransactionLock,
        XpenseDbContext dbContext,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var resourceId = await dbContext.EncryptedRecords.AsNoTracking()
            .Where(item => item.Id == id && item.ParentResourceId.HasValue)
            .Select(item => item.ParentResourceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!resourceId.HasValue)
            return TypedResults.NotFound();

        await using var resourceLock = await resourceTransactionLock.Acquire(resourceId.Value, cancellationToken);
        await using var groupLock = await groupTransactionLock.Acquire(groupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var record = await dbContext.EncryptedRecords.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerUserId == currentUser.Id,
            cancellationToken);
        if (record?.ParentResourceId != resourceId.Value ||
            !await IsActiveGroupMember(groupId, currentUser.Id, dbContext, cancellationToken))
            return TypedResults.NotFound();

        var resource = await dbContext.SharedResources.SingleOrDefaultAsync(
            item => item.Id == resourceId.Value && item.OwnerUserId == currentUser.Id,
            cancellationToken);
        if (resource is null)
            return TypedResults.NotFound();

        var envelope = await dbContext.RecordEnvelopes.SingleOrDefaultAsync(
            item => item.EncryptedRecordId == id && item.GroupId == groupId,
            cancellationToken);
        if (envelope is null)
            return TypedResults.NotFound();

        dbContext.RecordEnvelopes.Remove(envelope);

        if (id == resourceId.Value)
        {
            var grant = await dbContext.ResourceGrants.SingleOrDefaultAsync(
                item =>
                    item.GroupId == groupId &&
                    item.ResourceId == resourceId.Value &&
                    item.ResourceType == resource.Type &&
                    item.State == GrantState.Active,
                cancellationToken);
            if (grant is not null)
            {
                grant.Revoke(DateTime.UtcNow);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(new Response(true, RevocationWarning));
    }

    private static Task<bool> IsActiveGroupMember(
        Guid groupId,
        Guid userId,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.GroupMemberships.AnyAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId == userId &&
                membership.State == MembershipState.Active &&
                dbContext.Groups.Any(group => group.Id == groupId && !group.IsDeleted),
            cancellationToken);
}
