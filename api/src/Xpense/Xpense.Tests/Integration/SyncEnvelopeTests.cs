using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class SyncEnvelopeTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private string connectionString = null!;
    private Guid ownerId;
    private Guid memberId;
    private Guid groupId;
    private Guid resourceId;

    [SetUp]
    public async Task SetUp()
    {
        ownerId = Guid.CreateVersion7();
        memberId = Guid.CreateVersion7();
        groupId = Guid.CreateVersion7();
        resourceId = Guid.CreateVersion7();
        connectionString = await PostgresFixture.CreateDatabase();
        factory = new WebApiTestFactory(connectionString).AsUser(ownerId);
        client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.AddRange(User(ownerId), User(memberId));
        dbContext.Groups.Add(Group(groupId, ownerId, now));
        dbContext.GroupMemberships.AddRange(
            Membership(groupId, ownerId, MembershipRole.Owner, MembershipState.Active, now),
            Membership(groupId, memberId, MembershipRole.Member, MembershipState.Active, now));
        dbContext.SharedResources.Add(new SharedResource
        {
            Id = resourceId,
            Type = SharedResourceType.Account,
            OwnerUserId = ownerId,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task The_personal_owner_can_add_a_group_envelope_and_grant_viewer_access()
    {
        var record = await AddRecord(ownerId, resourceId);
        var request = new AddEnvelopeRequest(groupId, GrantPermission.Viewer, [7, 8, 9], [10, 11, 12], [13, 14], 1);

        var response = await client.PostAsJsonAsync($"/api/v1/sync/records/{record.Id}/envelopes", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var envelope = await dbContext.RecordEnvelopes.SingleAsync(item => item.EncryptedRecordId == record.Id && item.GroupId == groupId);
        var grant = await dbContext.ResourceGrants.SingleAsync(item => item.ResourceId == resourceId && item.GroupId == groupId);
        envelope.WrappedKey.Should().Equal(request.WrappedKey);
        envelope.Nonce.Should().Equal(request.Nonce);
        envelope.EncapsulatedKey.Should().Equal(request.EncapsulatedKey!);
        envelope.ProtocolVersion.Should().Be(request.ProtocolVersion);
        grant.State.Should().Be(GrantState.Active);
        grant.Permission.Should().Be(GrantPermission.Viewer);
        grant.KeyEnvelopeReference.Should().Be(envelope.Id);
        (await dbContext.SharedResources.SingleAsync(item => item.Id == resourceId)).OwnerUserId.Should().Be(ownerId);

        await using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        var changes = await memberClient.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");
        changes!.Records.Should().ContainSingle(item => item.Id == record.Id);
    }

    [Test]
    public async Task The_personal_owner_can_grant_editor_access()
    {
        var record = await AddRecord(ownerId, resourceId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Editor));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var grant = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().ResourceGrants
            .SingleAsync(item => item.GroupId == groupId && item.ResourceId == resourceId);
        grant.Permission.Should().Be(GrantPermission.Editor);
    }

    [Test]
    public async Task Adding_an_envelope_for_the_same_group_replaces_it_without_duplication()
    {
        var record = await AddRecord(ownerId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer, [1], [2], [3]));
        var replacement = ValidRequest(GrantPermission.Editor, [4, 5], [6, 7], [8, 9]);

        var response = await client.PostAsJsonAsync($"/api/v1/sync/records/{record.Id}/envelopes", replacement);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var envelope = await dbContext.RecordEnvelopes.SingleAsync(item => item.EncryptedRecordId == record.Id && item.GroupId == groupId);
        var grant = await dbContext.ResourceGrants.SingleAsync(item => item.ResourceId == resourceId && item.GroupId == groupId);
        (await dbContext.RecordEnvelopes.CountAsync(item => item.EncryptedRecordId == record.Id && item.GroupId == groupId))
            .Should().Be(1);
        envelope.WrappedKey.Should().Equal(replacement.WrappedKey);
        envelope.Nonce.Should().Equal(replacement.Nonce);
        envelope.EncapsulatedKey.Should().Equal(replacement.EncapsulatedKey!);
        grant.Permission.Should().Be(GrantPermission.Editor);
        grant.KeyEnvelopeReference.Should().Be(envelope.Id);
    }

    [Test]
    public async Task A_group_owner_who_does_not_own_the_record_receives_not_found()
    {
        var groupOwnerId = Guid.CreateVersion7();
        var groupOwnerGroupId = Guid.CreateVersion7();
        var record = await AddRecord(ownerId, resourceId);
        await AddGroupMember(groupOwnerGroupId, groupOwnerId, MembershipRole.Owner, MembershipState.Active);

        await using var actorFactory = new WebApiTestFactory(connectionString).AsUser(groupOwnerId);
        using var actorClient = actorFactory.CreateClient();
        var response = await actorClient.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer) with { GroupId = groupOwnerGroupId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task An_editor_who_owns_a_dependent_record_cannot_add_or_revoke_a_resource_envelope()
    {
        var resourceOwnerId = Guid.CreateVersion7();
        var foreignResourceId = Guid.CreateVersion7();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var now = DateTime.UtcNow;
            dbContext.Users.Add(User(resourceOwnerId));
            dbContext.SharedResources.Add(new SharedResource
            {
                Id = foreignResourceId,
                Type = SharedResourceType.Account,
                OwnerUserId = resourceOwnerId,
                CreatedAt = now
            });
            dbContext.ResourceGrants.Add(new ResourceGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = groupId,
                ResourceType = SharedResourceType.Account,
                ResourceId = foreignResourceId,
                Permission = GrantPermission.Editor,
                State = GrantState.Active,
                GrantedByUserId = resourceOwnerId,
                CreatedAt = now,
                UpdatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }
        var addTarget = await AddRecord(ownerId, foreignResourceId);
        var revokeTarget = await AddRecord(ownerId, foreignResourceId);

        var add = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{addTarget.Id}/envelopes",
            ValidRequest(GrantPermission.Editor));
        var envelopeId = await AddEnvelopeDirect(revokeTarget.Id, groupId);
        var revoke = await client.DeleteAsync($"/api/v1/sync/records/{revokeTarget.Id}/envelopes/{groupId}");

        add.StatusCode.Should().Be(HttpStatusCode.NotFound);
        revoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await verificationContext.RecordEnvelopes.AnyAsync(envelope => envelope.Id == envelopeId)).Should().BeTrue();
        (await verificationContext.ResourceGrants.SingleAsync(grant => grant.ResourceId == foreignResourceId))
            .State.Should().Be(GrantState.Active);
    }

    [Test]
    public async Task An_unrelated_user_receives_not_found()
    {
        var unrelatedUserId = Guid.CreateVersion7();
        var record = await AddRecord(ownerId, resourceId);
        await AddUser(unrelatedUserId);

        await using var actorFactory = new WebApiTestFactory(connectionString).AsUser(unrelatedUserId);
        using var actorClient = actorFactory.CreateClient();
        var response = await actorClient.DeleteAsync($"/api/v1/sync/records/{record.Id}/envelopes/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_target_group_requires_an_active_member_and_a_nondeleted_group()
    {
        var record = await AddRecord(ownerId, resourceId);
        await SetOwnerMembershipState(MembershipState.Revoked);

        var inactive = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));

        inactive.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SetOwnerMembershipState(MembershipState.Active);
        await SetGroupDeleted();
        var deleted = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));

        deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Invalid_envelope_values_are_rejected_before_they_are_stored()
    {
        var record = await AddRecord(ownerId, resourceId);

        var invalidProtocol = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer) with { ProtocolVersion = 2 });
        var invalidPermission = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest((GrantPermission)999));
        var oversized = await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer) with { WrappedKey = new byte[4097] });
        var invalidBase64 = await client.PostAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            new StringContent(
                $"{{\"groupId\":\"{groupId}\",\"permission\":0,\"wrappedKey\":\"not-base64!\",\"nonce\":\"AQ==\",\"encapsulatedKey\":\"Ag==\",\"protocolVersion\":1}}",
                Encoding.UTF8,
                "application/json"));

        invalidProtocol.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidPermission.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidBase64.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().RecordEnvelopes.CountAsync())
            .Should().Be(0);
    }

    [Test]
    public async Task Revoking_a_primary_record_envelope_hides_the_resource_and_preserves_owner_access()
    {
        var record = await AddRecord(ownerId, resourceId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));

        var revoke = await client.DeleteAsync($"/api/v1/sync/records/{record.Id}/envelopes/{groupId}");
        var body = await revoke.Content.ReadFromJsonAsync<RevokeResponse>();

        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.KeyRotationRequired.Should().BeTrue();
        body.Warning.Should().Contain("cannot erase");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.RecordEnvelopes.AnyAsync(item => item.EncryptedRecordId == record.Id && item.GroupId == groupId))
            .Should().BeFalse();
        var grant = await dbContext.ResourceGrants.SingleAsync(item => item.GroupId == groupId && item.ResourceId == resourceId);
        grant.State.Should().Be(GrantState.Revoked);
        grant.RevokedAt.Should().NotBeNull();
        grant.KeyEnvelopeReference.Should().BeNull();

        await using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        var memberChanges = await memberClient.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");
        var ownerChanges = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");
        memberChanges!.Records.Should().NotContain(item => item.Id == record.Id);
        ownerChanges!.Records.Should().ContainSingle(item => item.Id == record.Id);
    }

    [Test]
    public async Task Revoking_a_dependent_record_envelope_does_not_revoke_the_parent_resource_grant()
    {
        var primary = await AddRecord(ownerId, resourceId, resourceId);
        var dependent = await AddRecord(ownerId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{primary.Id}/envelopes",
            ValidRequest(GrantPermission.Editor));
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{dependent.Id}/envelopes",
            ValidRequest(GrantPermission.Editor));

        var revoke = await client.DeleteAsync($"/api/v1/sync/records/{dependent.Id}/envelopes/{groupId}");

        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var grant = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().ResourceGrants
            .SingleAsync(item => item.GroupId == groupId && item.ResourceId == resourceId);
        grant.State.Should().Be(GrantState.Active);

        await using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        var changes = await memberClient.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");
        changes!.Records.Select(item => item.Id).Should().Contain([primary.Id, dependent.Id]);
    }

    [Test]
    public async Task A_grant_and_envelope_do_not_leak_to_another_group()
    {
        var otherGroupId = Guid.CreateVersion7();
        var otherMemberId = Guid.CreateVersion7();
        var record = await AddRecord(ownerId, resourceId);
        await AddGroupMember(otherGroupId, otherMemberId, MembershipRole.Member, MembershipState.Active);

        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));

        await using var otherFactory = new WebApiTestFactory(connectionString).AsUser(otherMemberId);
        using var otherClient = otherFactory.CreateClient();
        var changes = await otherClient.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        changes!.Records.Should().NotContain(item => item.Id == record.Id);
    }

    [Test]
    public async Task A_deleted_group_envelope_is_not_returned_to_the_record_owner()
    {
        var record = await AddRecord(ownerId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));
        await SetGroupDeleted();

        var changes = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        changes!.Records.Single(item => item.Id == record.Id).Envelopes
            .Should().NotContain(envelope => envelope.GroupId == groupId);
    }

    [Test]
    public async Task A_revoked_grant_envelope_is_not_returned_to_the_record_owner()
    {
        var record = await AddRecord(ownerId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));
        await SetGrantState(groupId, resourceId, GrantState.Revoked);

        var changes = await client.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        changes!.Records.Single(item => item.Id == record.Id).Envelopes
            .Should().NotContain(envelope => envelope.GroupId == groupId);
    }

    [Test]
    public async Task A_record_readable_through_one_group_does_not_return_another_groups_envelope()
    {
        var otherGroupId = Guid.CreateVersion7();
        var record = await AddRecord(ownerId, resourceId);
        await client.PostAsJsonAsync(
            $"/api/v1/sync/records/{record.Id}/envelopes",
            ValidRequest(GrantPermission.Viewer));
        await AddExistingUserToNewGroup(otherGroupId, memberId);
        await AddEnvelopeDirect(record.Id, otherGroupId);

        await using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        var changes = await memberClient.GetFromJsonAsync<ChangesResponse>("/api/v1/sync/changes");

        changes!.Records.Single(item => item.Id == record.Id).Envelopes.Select(envelope => envelope.GroupId)
            .Should().Equal(groupId);
    }

    private async Task<EncryptedRecord> AddRecord(Guid ownerUserId, Guid parentResourceId, Guid? recordId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var record = new EncryptedRecord
        {
            Id = recordId ?? Guid.CreateVersion7(),
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerUserId,
            ParentResourceId = parentResourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1],
            Ciphertext = [2],
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.EncryptedRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    private async Task AddGroupMember(
        Guid id,
        Guid userId,
        MembershipRole role,
        MembershipState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.Add(User(userId));
        dbContext.Groups.Add(Group(id, userId, now));
        dbContext.GroupMemberships.Add(Membership(id, userId, role, state, now));
        await dbContext.SaveChangesAsync();
    }

    private async Task AddUser(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Users.Add(User(id));
        await dbContext.SaveChangesAsync();
    }

    private async Task AddExistingUserToNewGroup(Guid id, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Groups.Add(Group(id, userId, now));
        dbContext.GroupMemberships.Add(Membership(id, userId, MembershipRole.Owner, MembershipState.Active, now));
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> AddEnvelopeDirect(Guid recordId, Guid targetGroupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var envelope = new RecordEnvelope
        {
            Id = Guid.CreateVersion7(),
            EncryptedRecordId = recordId,
            GroupId = targetGroupId,
            WrappedKey = [21],
            Nonce = [22],
            EncapsulatedKey = [23],
            ProtocolVersion = 1
        };
        dbContext.RecordEnvelopes.Add(envelope);
        await dbContext.SaveChangesAsync();
        return envelope.Id;
    }

    private async Task SetGrantState(Guid targetGroupId, Guid targetResourceId, GrantState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var grant = await dbContext.ResourceGrants.SingleAsync(
            item => item.GroupId == targetGroupId && item.ResourceId == targetResourceId);
        grant.State = state;
        grant.RevokedAt = state == GrantState.Revoked ? DateTime.UtcNow : null;
        grant.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    private async Task SetOwnerMembershipState(MembershipState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var membership = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().GroupMemberships
            .SingleAsync(item => item.GroupId == groupId && item.UserId == ownerId);
        membership.State = state;
        membership.RevokedAt = state == MembershipState.Revoked ? DateTime.UtcNow : null;
        membership.UpdatedAt = DateTime.UtcNow;
        await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().SaveChangesAsync();
    }

    private async Task SetGroupDeleted()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var group = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Groups.SingleAsync(item => item.Id == groupId);
        group.IsDeleted = true;
        group.UpdatedAt = DateTime.UtcNow;
        await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().SaveChangesAsync();
    }

    private AddEnvelopeRequest ValidRequest(
        GrantPermission permission,
        byte[]? wrappedKey = null,
        byte[]? nonce = null,
        byte[]? encapsulatedKey = null) =>
        new(groupId, permission, wrappedKey ?? [7, 8, 9], nonce ?? [10, 11, 12], encapsulatedKey ?? [13, 14], 1);

    private static XpenseUser User(Guid id) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = DateTime.UtcNow
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
        UpdatedAt = now
    };

    private sealed record AddEnvelopeRequest(
        Guid GroupId,
        GrantPermission Permission,
        byte[] WrappedKey,
        byte[] Nonce,
        byte[]? EncapsulatedKey,
        int ProtocolVersion);

    private sealed record ChangesResponse(ChangeRecord[] Records);

    private sealed record ChangeRecord(Guid Id, EnvelopeResponse[] Envelopes);

    private sealed record EnvelopeResponse(Guid? GroupId);

    private sealed record RevokeResponse(bool KeyRotationRequired, string Warning);
}
