using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Infrastructure.Authorization;

public sealed class AccessRules(XpenseDbContext dbContext, ICurrentUser currentUser)
{
    public Task<bool> IsActiveMember(Guid groupId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Task.FromResult(false);

        var userId = currentUser.Id;

        return dbContext.GroupMemberships.AsNoTracking().AnyAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId == userId &&
                membership.State == MembershipState.Active &&
                dbContext.Groups.Any(group =>
                    group.Id == membership.GroupId &&
                    !group.IsDeleted),
            cancellationToken);
    }

    public Task<bool> IsGroupOwner(Guid groupId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Task.FromResult(false);

        var userId = currentUser.Id;

        return dbContext.Groups.AsNoTracking().AnyAsync(
            group =>
                group.Id == groupId &&
                group.OwnerUserId == userId &&
                !group.IsDeleted &&
                dbContext.GroupMemberships.Any(membership =>
                    membership.GroupId == group.Id &&
                    membership.UserId == userId &&
                    membership.Role == MembershipRole.Owner &&
                    membership.State == MembershipState.Active),
            cancellationToken);
    }

    public Task<bool> CanRead(
        SharedResourceType type,
        Guid resourceId,
        CancellationToken cancellationToken) =>
        CanAccess(type, resourceId, false, cancellationToken);

    public Task<bool> CanEdit(
        SharedResourceType type,
        Guid resourceId,
        CancellationToken cancellationToken) =>
        CanAccess(type, resourceId, true, cancellationToken);

    public Task<bool> CanReadAny(
        SharedResourceType type,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || resourceIds.Count == 0)
            return Task.FromResult(false);

        var userId = currentUser.Id;
        var distinctIds = resourceIds.Distinct().ToArray();

        return AccessibleResources(type, false, userId)
            .AnyAsync(resource => distinctIds.Contains(resource.Id), cancellationToken);
    }

    public async Task<bool> CanEditAll(
        SharedResourceType type,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || resourceIds.Count == 0)
            return false;

        var userId = currentUser.Id;
        var distinctIds = resourceIds.Distinct().ToArray();
        var writableCount = await AccessibleResources(type, true, userId)
            .Where(resource => distinctIds.Contains(resource.Id))
            .Select(resource => resource.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        return writableCount == distinctIds.Length;
    }

    public Task<Guid[]> ReadableResourceIds(
        SharedResourceType type,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Task.FromResult(Array.Empty<Guid>());

        var userId = currentUser.Id;

        return AccessibleResources(type, false, userId)
            .Select(resource => resource.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private Task<bool> CanAccess(
        SharedResourceType type,
        Guid resourceId,
        bool requireEditor,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Task.FromResult(false);

        var userId = currentUser.Id;

        return AccessibleResources(type, requireEditor, userId)
            .AnyAsync(resource => resource.Id == resourceId, cancellationToken);
    }

    private IQueryable<SharedResource> AccessibleResources(
        SharedResourceType type,
        bool requireEditor,
        Guid userId) =>
        dbContext.SharedResources.AsNoTracking().Where(resource =>
            resource.Type == type &&
            (resource.OwnerUserId == userId ||
             dbContext.ResourceGrants.Any(grant =>
                 grant.ResourceId == resource.Id &&
                 grant.ResourceType == resource.Type &&
                 grant.State == GrantState.Active &&
                 (!requireEditor || grant.Permission == GrantPermission.Editor) &&
                 dbContext.Groups.Any(group =>
                     group.Id == grant.GroupId &&
                     !group.IsDeleted) &&
                 dbContext.GroupMemberships.Any(membership =>
                     membership.GroupId == grant.GroupId &&
                     membership.UserId == userId &&
                     membership.State == MembershipState.Active))));
}
