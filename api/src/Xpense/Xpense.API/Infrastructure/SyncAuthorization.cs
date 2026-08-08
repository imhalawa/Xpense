using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Infrastructure;

public sealed class SyncAuthorization(XpenseDbContext dbContext, ICurrentUser currentUser)
{
    public IQueryable<EncryptedRecord> ReadableRecords() => AccessibleRecords(false);

    public IQueryable<EncryptedRecord> WritableRecords() => AccessibleRecords(true);

    public Task<bool> CanRead(Guid encryptedRecordId, CancellationToken cancellationToken = default) =>
        ReadableRecords().AnyAsync(record => record.Id == encryptedRecordId, cancellationToken);

    public Task<bool> CanWrite(Guid encryptedRecordId, CancellationToken cancellationToken = default) =>
        WritableRecords().AnyAsync(record => record.Id == encryptedRecordId, cancellationToken);

    public Task<bool> CanWriteResource(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        return dbContext.SharedResources.AsNoTracking().AnyAsync(
            resource =>
                resource.Id == resourceId &&
                (resource.OwnerUserId == userId ||
                 dbContext.ResourceGrants.Any(grant =>
                     grant.ResourceId == resource.Id &&
                     grant.ResourceType == resource.Type &&
                     grant.State == GrantState.Active &&
                     grant.Permission == GrantPermission.Editor &&
                     dbContext.Groups.Any(group => group.Id == grant.GroupId && !group.IsDeleted) &&
                     dbContext.GroupMemberships.Any(membership =>
                         membership.GroupId == grant.GroupId &&
                         membership.UserId == userId &&
                         membership.State == MembershipState.Active))),
            cancellationToken);
    }

    private IQueryable<EncryptedRecord> AccessibleRecords(bool requireEditor)
    {
        var userId = currentUser.Id;

        return dbContext.EncryptedRecords
            .AsNoTracking()
            .Where(record =>
                record.OwnerUserId == userId ||
                record.ParentResourceId.HasValue &&
                dbContext.ResourceGrants.Any(grant =>
                    grant.ResourceId == record.ParentResourceId.Value &&
                    grant.State == GrantState.Active &&
                    (!requireEditor || grant.Permission == GrantPermission.Editor) &&
                    dbContext.Groups.Any(group => group.Id == grant.GroupId && !group.IsDeleted) &&
                    dbContext.GroupMemberships.Any(membership =>
                        membership.GroupId == grant.GroupId &&
                        membership.UserId == userId &&
                        membership.State == MembershipState.Active)));
    }
}
