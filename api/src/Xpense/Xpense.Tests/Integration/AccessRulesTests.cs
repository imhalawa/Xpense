using System.Security.Claims;
using System.Data.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class AccessRulesTests
{
    private XpenseDbContext dbContext = null!;
    private SeededAccess seeded;

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
    public async Task The_private_owner_can_read_and_edit()
    {
        var rules = For(seeded.ResourceOwnerId);

        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeTrue();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeTrue();
    }

    [Test]
    public async Task A_group_viewer_can_read_and_cannot_edit()
    {
        var rules = For(seeded.ViewerId);

        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeTrue();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task A_group_editor_can_read_and_edit()
    {
        var rules = For(seeded.EditorId);

        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeTrue();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeTrue();
    }

    [Test]
    public async Task A_group_owner_without_a_grant_can_do_neither()
    {
        var rules = For(seeded.GroupOwnerId);

        (await rules.IsGroupOwner(seeded.OwnerOnlyGroupId, default)).Should().BeTrue();
        (await rules.IsActiveMember(seeded.OwnerOnlyGroupId, default)).Should().BeTrue();
        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task Stale_group_owner_metadata_with_a_revoked_owner_membership_is_not_ownership()
    {
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == seeded.OwnerOnlyGroupId &&
            membership.UserId == seeded.GroupOwnerId);
        membership.State = MembershipState.Revoked;
        membership.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        (await For(seeded.GroupOwnerId).IsGroupOwner(seeded.OwnerOnlyGroupId, default)).Should().BeFalse();
    }

    [Test]
    public async Task Stale_group_owner_metadata_without_a_membership_is_not_ownership()
    {
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == seeded.OwnerOnlyGroupId &&
            membership.UserId == seeded.GroupOwnerId);
        dbContext.GroupMemberships.Remove(membership);
        await dbContext.SaveChangesAsync();

        (await For(seeded.GroupOwnerId).IsGroupOwner(seeded.OwnerOnlyGroupId, default)).Should().BeFalse();
    }

    [Test]
    public async Task Stale_group_owner_metadata_with_an_active_member_role_is_not_ownership()
    {
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == seeded.OwnerOnlyGroupId &&
            membership.UserId == seeded.GroupOwnerId);
        membership.Role = MembershipRole.Member;
        await dbContext.SaveChangesAsync();

        (await For(seeded.GroupOwnerId).IsGroupOwner(seeded.OwnerOnlyGroupId, default)).Should().BeFalse();
    }

    [Test]
    public async Task A_revoked_member_can_do_neither()
    {
        var rules = For(seeded.RevokedMemberId);

        (await rules.IsActiveMember(seeded.RevokedGroupId, default)).Should().BeFalse();
        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task An_unrelated_user_can_do_neither()
    {
        var rules = For(seeded.UnrelatedUserId);

        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task A_member_of_two_groups_sees_nothing_from_a_different_granted_group()
    {
        var rules = For(seeded.MultiGroupMemberId);

        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanRead(SharedResourceType.Account, seeded.MultiReadableResourceId, default)).Should().BeTrue();
        (await rules.ReadableResourceIds(SharedResourceType.Account, default)).Should()
            .Equal(seeded.MultiReadableResourceId);
    }

    [Test]
    public async Task A_deleted_group_grants_nothing()
    {
        var rules = For(seeded.DeletedGroupMemberId);

        (await rules.IsActiveMember(seeded.DeletedGroupId, default)).Should().BeFalse();
        (await rules.IsGroupOwner(seeded.DeletedGroupId, default)).Should().BeFalse();
        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task A_revoked_grant_and_a_mismatched_resource_type_grant_nothing()
    {
        var viewer = For(seeded.ViewerId);
        var owner = For(seeded.ResourceOwnerId);

        (await viewer.CanRead(SharedResourceType.Account, seeded.RevokedGrantResourceId, default)).Should().BeFalse();
        (await viewer.CanEdit(SharedResourceType.Account, seeded.RevokedGrantResourceId, default)).Should().BeFalse();
        (await viewer.CanRead(SharedResourceType.Account, seeded.MismatchedGrantResourceId, default)).Should().BeFalse();
        (await viewer.CanRead(SharedResourceType.Budget, seeded.MismatchedGrantResourceId, default)).Should().BeFalse();
        (await viewer.CanRead(SharedResourceType.Budget, seeded.ResourceId, default)).Should().BeFalse();
        (await owner.CanRead(SharedResourceType.Account, seeded.BudgetResourceId, default)).Should().BeFalse();
    }

    [Test]
    public async Task A_transfer_is_readable_when_one_side_is_readable()
    {
        var rules = For(seeded.ViewerId);

        (await rules.CanReadAny(
            SharedResourceType.Account,
            [seeded.ResourceId, seeded.PrivateCounterpartyId],
            default)).Should().BeTrue();
        (await rules.CanReadAny(
            SharedResourceType.Account,
            [seeded.PrivateCounterpartyId],
            default)).Should().BeFalse();
    }

    [Test]
    public async Task A_transfer_is_editable_only_when_every_distinct_side_is_editable()
    {
        var rules = For(seeded.EditorId);

        (await rules.CanEditAll(
            SharedResourceType.Account,
            [seeded.ResourceId, seeded.ResourceId],
            default)).Should().BeTrue();
        (await rules.CanEditAll(
            SharedResourceType.Account,
            [seeded.ResourceId, seeded.PrivateCounterpartyId],
            default)).Should().BeFalse();
        (await rules.CanEditAll(
            SharedResourceType.Account,
            [seeded.ResourceId, Guid.CreateVersion7()],
            default)).Should().BeFalse();
    }

    [Test]
    public async Task Empty_transfer_inputs_are_never_authorized()
    {
        var rules = For(seeded.ResourceOwnerId);

        (await rules.CanReadAny(SharedResourceType.Account, [], default)).Should().BeFalse();
        (await rules.CanEditAll(SharedResourceType.Account, [], default)).Should().BeFalse();
    }

    [Test]
    public async Task Readable_resource_ids_are_type_scoped_and_distinct()
    {
        var viewer = For(seeded.ViewerId);
        var owner = For(seeded.ResourceOwnerId);

        (await viewer.ReadableResourceIds(SharedResourceType.Account, default)).Should()
            .Equal(seeded.ResourceId);
        (await viewer.ReadableResourceIds(SharedResourceType.Budget, default)).Should().BeEmpty();
        (await owner.ReadableResourceIds(SharedResourceType.Budget, default)).Should()
            .Equal(seeded.BudgetResourceId);
    }

    [Test]
    public async Task Unauthenticated_access_returns_false_without_reading_an_identifier()
    {
        var rules = new AccessRules(dbContext, new TestCurrentUser(Guid.Empty, false));

        (await rules.IsActiveMember(seeded.ViewerGroupId, default)).Should().BeFalse();
        (await rules.IsGroupOwner(seeded.ViewerGroupId, default)).Should().BeFalse();
        (await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default)).Should().BeFalse();
        (await rules.CanReadAny(SharedResourceType.Account, [seeded.ResourceId], default)).Should().BeFalse();
        (await rules.CanEditAll(SharedResourceType.Account, [seeded.ResourceId], default)).Should().BeFalse();
        (await rules.ReadableResourceIds(SharedResourceType.Account, default)).Should().BeEmpty();
    }

    [Test]
    public void Current_user_requires_an_authenticated_principal_with_a_guid_identifier()
    {
        var validId = Guid.CreateVersion7();
        var valid = CurrentUser(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, validId.ToString())],
            "Test"));

        valid.IsAuthenticated.Should().BeTrue();
        valid.Id.Should().Be(validId);

        var anonymous = CurrentUser(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, validId.ToString())]));

        anonymous.IsAuthenticated.Should().BeFalse();

        var malformed = CurrentUser(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "not-a-guid")],
            "Test"));

        malformed.IsAuthenticated.Should().BeFalse();
        var readMalformedId = () => malformed.Id;
        readMalformedId.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task Every_public_authorization_decision_executes_one_database_query()
    {
        var interceptor = new QueryCountingInterceptor();
        await using var countingContext = new XpenseDbContext(
            new DbContextOptionsBuilder<XpenseDbContext>()
                .UseNpgsql(dbContext.Database.GetConnectionString())
                .AddInterceptors(interceptor)
                .Options);
        var rules = new AccessRules(countingContext, new TestCurrentUser(seeded.EditorId));

        await rules.IsActiveMember(seeded.ViewerGroupId, default);
        interceptor.TakeCount().Should().Be(1);
        await rules.IsGroupOwner(seeded.OwnerOnlyGroupId, default);
        interceptor.TakeCount().Should().Be(1);
        await rules.CanRead(SharedResourceType.Account, seeded.ResourceId, default);
        interceptor.TakeCount().Should().Be(1);
        await rules.CanEdit(SharedResourceType.Account, seeded.ResourceId, default);
        interceptor.TakeCount().Should().Be(1);
        await rules.CanReadAny(SharedResourceType.Account, [seeded.ResourceId], default);
        interceptor.TakeCount().Should().Be(1);
        await rules.CanEditAll(SharedResourceType.Account, [seeded.ResourceId], default);
        interceptor.TakeCount().Should().Be(1);
        await rules.ReadableResourceIds(SharedResourceType.Account, default);
        interceptor.TakeCount().Should().Be(1);
    }

    private AccessRules For(Guid userId) => new(dbContext, new TestCurrentUser(userId));

    private static HttpContextCurrentUser CurrentUser(ClaimsIdentity identity) => new(
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        });

    private static async Task<SeededAccess> Seed(XpenseDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var resourceOwnerId = Guid.CreateVersion7();
        var viewerId = Guid.CreateVersion7();
        var editorId = Guid.CreateVersion7();
        var groupOwnerId = Guid.CreateVersion7();
        var revokedMemberId = Guid.CreateVersion7();
        var unrelatedUserId = Guid.CreateVersion7();
        var deletedGroupMemberId = Guid.CreateVersion7();
        var multiGroupMemberId = Guid.CreateVersion7();
        var viewerGroupId = Guid.CreateVersion7();
        var editorGroupId = Guid.CreateVersion7();
        var ownerOnlyGroupId = Guid.CreateVersion7();
        var revokedGroupId = Guid.CreateVersion7();
        var deletedGroupId = Guid.CreateVersion7();
        var multiNoGrantGroupId = Guid.CreateVersion7();
        var multiValidGroupId = Guid.CreateVersion7();
        var grantWithoutMembershipGroupId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var privateCounterpartyId = Guid.CreateVersion7();
        var budgetResourceId = Guid.CreateVersion7();
        var revokedGrantResourceId = Guid.CreateVersion7();
        var mismatchedGrantResourceId = Guid.CreateVersion7();
        var multiReadableResourceId = Guid.CreateVersion7();

        dbContext.Users.AddRange(
            User(resourceOwnerId, now),
            User(viewerId, now),
            User(editorId, now),
            User(groupOwnerId, now),
            User(revokedMemberId, now),
            User(unrelatedUserId, now),
            User(deletedGroupMemberId, now),
            User(multiGroupMemberId, now));
        dbContext.Groups.AddRange(
            Group(viewerGroupId, groupOwnerId, false, now),
            Group(editorGroupId, groupOwnerId, false, now),
            Group(ownerOnlyGroupId, groupOwnerId, false, now),
            Group(revokedGroupId, groupOwnerId, false, now),
            Group(deletedGroupId, deletedGroupMemberId, true, now),
            Group(multiNoGrantGroupId, groupOwnerId, false, now),
            Group(multiValidGroupId, groupOwnerId, false, now),
            Group(grantWithoutMembershipGroupId, groupOwnerId, false, now));
        dbContext.GroupMemberships.AddRange(
            Membership(viewerGroupId, viewerId, MembershipRole.Member, MembershipState.Active, now),
            Membership(editorGroupId, editorId, MembershipRole.Member, MembershipState.Active, now),
            Membership(ownerOnlyGroupId, groupOwnerId, MembershipRole.Owner, MembershipState.Active, now),
            Membership(revokedGroupId, revokedMemberId, MembershipRole.Member, MembershipState.Revoked, now),
            Membership(deletedGroupId, deletedGroupMemberId, MembershipRole.Owner, MembershipState.Active, now),
            Membership(multiNoGrantGroupId, multiGroupMemberId, MembershipRole.Member, MembershipState.Active, now),
            Membership(multiValidGroupId, multiGroupMemberId, MembershipRole.Member, MembershipState.Active, now));
        dbContext.SharedResources.AddRange(
            Resource(resourceId, resourceOwnerId, SharedResourceType.Account, now),
            Resource(privateCounterpartyId, unrelatedUserId, SharedResourceType.Account, now),
            Resource(budgetResourceId, resourceOwnerId, SharedResourceType.Budget, now),
            Resource(revokedGrantResourceId, resourceOwnerId, SharedResourceType.Account, now),
            Resource(mismatchedGrantResourceId, resourceOwnerId, SharedResourceType.Account, now),
            Resource(multiReadableResourceId, resourceOwnerId, SharedResourceType.Account, now));
        dbContext.ResourceGrants.AddRange(
            Grant(viewerGroupId, resourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Viewer, GrantState.Active, now),
            Grant(editorGroupId, resourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Editor, GrantState.Active, now),
            Grant(revokedGroupId, resourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Editor, GrantState.Active, now),
            Grant(deletedGroupId, resourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Editor, GrantState.Active, now),
            Grant(viewerGroupId, revokedGrantResourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Editor, GrantState.Revoked, now),
            Grant(viewerGroupId, mismatchedGrantResourceId, SharedResourceType.Budget, resourceOwnerId, GrantPermission.Viewer, GrantState.Active, now),
            Grant(multiValidGroupId, multiReadableResourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Viewer, GrantState.Active, now),
            Grant(grantWithoutMembershipGroupId, resourceId, SharedResourceType.Account, resourceOwnerId, GrantPermission.Viewer, GrantState.Active, now));
        await dbContext.SaveChangesAsync();

        return new SeededAccess(
            resourceOwnerId,
            viewerId,
            editorId,
            groupOwnerId,
            revokedMemberId,
            unrelatedUserId,
            deletedGroupMemberId,
            multiGroupMemberId,
            viewerGroupId,
            ownerOnlyGroupId,
            revokedGroupId,
            deletedGroupId,
            resourceId,
            privateCounterpartyId,
            budgetResourceId,
            revokedGrantResourceId,
            mismatchedGrantResourceId,
            multiReadableResourceId);
    }

    private static XpenseUser User(Guid id, DateTime now) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = now
    };

    private static Xpense.Domain.Entities.Group Group(
        Guid id,
        Guid ownerUserId,
        bool isDeleted,
        DateTime now) => new()
    {
        Id = id,
        OwnerUserId = ownerUserId,
        NameCiphertext = [1],
        NameNonce = [2],
        ProtocolVersion = 1,
        IsDeleted = isDeleted,
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

    private static SharedResource Resource(
        Guid id,
        Guid ownerUserId,
        SharedResourceType type,
        DateTime now) => new()
    {
        Id = id,
        OwnerUserId = ownerUserId,
        Type = type,
        CreatedAt = now
    };

    private static ResourceGrant Grant(
        Guid groupId,
        Guid resourceId,
        SharedResourceType resourceType,
        Guid grantedByUserId,
        GrantPermission permission,
        GrantState state,
        DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        ResourceType = resourceType,
        ResourceId = resourceId,
        Permission = permission,
        State = state,
        GrantedByUserId = grantedByUserId,
        CreatedAt = now,
        UpdatedAt = now,
        RevokedAt = state == GrantState.Revoked ? now : null
    };

    private sealed class TestCurrentUser(Guid id, bool isAuthenticated = true) : ICurrentUser
    {
        public Guid Id => IsAuthenticated
            ? id
            : throw new InvalidOperationException("Unauthenticated test user identifiers must not be read");

        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class QueryCountingInterceptor : DbCommandInterceptor
    {
        private int count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return ValueTask.FromResult(result);
        }

        public int TakeCount() => Interlocked.Exchange(ref count, 0);
    }

    private sealed record SeededAccess(
        Guid ResourceOwnerId,
        Guid ViewerId,
        Guid EditorId,
        Guid GroupOwnerId,
        Guid RevokedMemberId,
        Guid UnrelatedUserId,
        Guid DeletedGroupMemberId,
        Guid MultiGroupMemberId,
        Guid ViewerGroupId,
        Guid OwnerOnlyGroupId,
        Guid RevokedGroupId,
        Guid DeletedGroupId,
        Guid ResourceId,
        Guid PrivateCounterpartyId,
        Guid BudgetResourceId,
        Guid RevokedGrantResourceId,
        Guid MismatchedGrantResourceId,
        Guid MultiReadableResourceId);
}
