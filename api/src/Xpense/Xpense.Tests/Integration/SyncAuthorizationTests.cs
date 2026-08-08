using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class SyncAuthorizationTests
{
    private XpenseDbContext dbContext = null!;
    private SeededAuthorization seeded;

    [SetUp]
    public async Task SetUp()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(await PostgresFixture.CreateDatabase())
        {
            Pooling = false
        }.ConnectionString;
        dbContext = new XpenseDbContext(
            new DbContextOptionsBuilder<XpenseDbContext>().UseNpgsql(connectionString).Options);
        seeded = await Seed(dbContext);
    }

    [TearDown]
    public async Task TearDown() => await dbContext.DisposeAsync();

    [Test]
    public async Task A_private_owner_can_read_and_write()
    {
        var authorization = For(seeded.ResourceOwnerId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeTrue();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeTrue();
    }

    [Test]
    public async Task A_group_viewer_can_read_but_not_write()
    {
        var authorization = For(seeded.ViewerId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeTrue();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeFalse();
    }

    [Test]
    public async Task A_group_editor_can_read_and_write()
    {
        var authorization = For(seeded.EditorId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeTrue();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeTrue();
    }

    [Test]
    public async Task A_group_owner_without_a_grant_cannot_read_or_write()
    {
        var authorization = For(seeded.GroupOwnerId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeFalse();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeFalse();
    }

    [Test]
    public async Task A_revoked_member_cannot_read_or_write()
    {
        var authorization = For(seeded.RevokedMemberId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeFalse();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeFalse();
    }

    [Test]
    public async Task An_unrelated_user_cannot_read_or_write()
    {
        var authorization = For(seeded.UnrelatedUserId);

        (await authorization.CanRead(seeded.RecordId)).Should().BeFalse();
        (await authorization.CanWrite(seeded.RecordId)).Should().BeFalse();
    }

    private SyncAuthorization For(Guid userId) => new(dbContext, new TestCurrentUser(userId));

    private static async Task<SeededAuthorization> Seed(XpenseDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var resourceOwnerId = Guid.CreateVersion7();
        var viewerId = Guid.CreateVersion7();
        var editorId = Guid.CreateVersion7();
        var groupOwnerId = Guid.CreateVersion7();
        var revokedMemberId = Guid.CreateVersion7();
        var unrelatedUserId = Guid.CreateVersion7();
        var viewerGroupId = Guid.CreateVersion7();
        var editorGroupId = Guid.CreateVersion7();
        var ownerWithoutGrantGroupId = Guid.CreateVersion7();
        var revokedGroupId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();

        dbContext.Users.AddRange(
            User(resourceOwnerId, now),
            User(viewerId, now),
            User(editorId, now),
            User(groupOwnerId, now),
            User(revokedMemberId, now),
            User(unrelatedUserId, now));

        dbContext.Groups.AddRange(
            Group(viewerGroupId, groupOwnerId, now),
            Group(editorGroupId, groupOwnerId, now),
            Group(ownerWithoutGrantGroupId, groupOwnerId, now),
            Group(revokedGroupId, groupOwnerId, now));

        dbContext.GroupMemberships.AddRange(
            Membership(viewerGroupId, viewerId, MembershipRole.Member, MembershipState.Active, now),
            Membership(editorGroupId, editorId, MembershipRole.Member, MembershipState.Active, now),
            Membership(ownerWithoutGrantGroupId, groupOwnerId, MembershipRole.Owner, MembershipState.Active, now),
            Membership(revokedGroupId, revokedMemberId, MembershipRole.Member, MembershipState.Revoked, now));

        dbContext.SharedResources.Add(new SharedResource
        {
            Id = resourceId,
            Type = SharedResourceType.Account,
            OwnerUserId = resourceOwnerId,
            CreatedAt = now
        });

        dbContext.ResourceGrants.AddRange(
            Grant(viewerGroupId, resourceId, resourceOwnerId, GrantPermission.Viewer, now),
            Grant(editorGroupId, resourceId, resourceOwnerId, GrantPermission.Editor, now),
            Grant(revokedGroupId, resourceId, resourceOwnerId, GrantPermission.Editor, now));

        dbContext.EncryptedRecords.Add(new EncryptedRecord
        {
            Id = recordId,
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = resourceOwnerId,
            ParentResourceId = resourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1, 2, 3],
            Ciphertext = [4, 5, 6],
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();

        return new SeededAuthorization(
            recordId,
            resourceOwnerId,
            viewerId,
            editorId,
            groupOwnerId,
            revokedMemberId,
            unrelatedUserId);
    }

    private static XpenseUser User(Guid id, DateTime now) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = now
    };

    private static Xpense.Domain.Entities.Group Group(Guid id, Guid ownerUserId, DateTime now) => new()
    {
        Id = id,
        OwnerUserId = ownerUserId,
        NameCiphertext = [1],
        NameNonce = [2],
        ProtocolVersion = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static GroupMembership Membership(
        Guid groupId,
        Guid userId,
        MembershipRole role,
        MembershipState state,
        DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        UserId = userId,
        Role = role,
        State = state,
        EnvelopeProtocolVersion = 1,
        CreatedAt = now,
        UpdatedAt = now,
        RevokedAt = state == MembershipState.Revoked ? now : null
    };

    private static ResourceGrant Grant(
        Guid groupId,
        Guid resourceId,
        Guid grantedByUserId,
        GrantPermission permission,
        DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        ResourceType = SharedResourceType.Account,
        ResourceId = resourceId,
        Permission = permission,
        State = GrantState.Active,
        GrantedByUserId = grantedByUserId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed record SeededAuthorization(
        Guid RecordId,
        Guid ResourceOwnerId,
        Guid ViewerId,
        Guid EditorId,
        Guid GroupOwnerId,
        Guid RevokedMemberId,
        Guid UnrelatedUserId);

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id { get; } = id;
    }
}
