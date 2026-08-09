using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class ResourceGrantTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private string connectionString = null!;
    private Guid ownerId;
    private Guid memberId;
    private Guid unrelatedId;
    private Guid groupId;
    private Guid otherGroupId;
    private Guid resourceId;
    private Guid recordId;
    private ResourceLockAttemptInterceptor resourceLockInterceptor = null!;

    [SetUp]
    public async Task SetUp()
    {
        connectionString = await PostgresFixture.CreateDatabase();
        ownerId = Guid.CreateVersion7();
        memberId = Guid.CreateVersion7();
        unrelatedId = Guid.CreateVersion7();
        groupId = Guid.CreateVersion7();
        otherGroupId = Guid.CreateVersion7();
        resourceId = Guid.CreateVersion7();
        recordId = Guid.CreateVersion7();
        resourceLockInterceptor = new ResourceLockAttemptInterceptor();
        factory = new WebApiTestFactory(connectionString, resourceLockInterceptor).AsUser(ownerId);
        client = factory.CreateClient();
        await Seed();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [TestCase(GrantPermission.Viewer)]
    [TestCase(GrantPermission.Editor)]
    public async Task The_resource_owner_can_grant_a_group_access(GrantPermission permission)
    {
        var keyEnvelopeReference = Guid.CreateVersion7();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            new CreateGrantRequest(
                SharedResourceType.Account,
                resourceId,
                permission,
                keyEnvelopeReference));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeTrue();
        var created = await response.Content.ReadFromJsonAsync<ResourceGrantContract>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.GroupId.Should().Be(groupId);
        created.ResourceType.Should().Be(SharedResourceType.Account);
        created.ResourceId.Should().Be(resourceId);
        created.Permission.Should().Be(permission);
        created.State.Should().Be(GrantState.Active);
        created.GrantedByUserId.Should().Be(ownerId);
        created.KeyEnvelopeReference.Should().Be(keyEnvelopeReference);
        created.CreatedAt.Should().NotBe(default);
        created.UpdatedAt.Should().Be(created.CreatedAt);
        created.RevokedAt.Should().BeNull();
        response.Headers.Location.AbsolutePath.Should().Be(
            $"/api/v1/groups/{groupId}/grants/{created.Id}");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var stored = await dbContext.ResourceGrants.SingleAsync();
        stored.GrantedByUserId.Should().Be(ownerId);
        stored.RevokedAt.Should().BeNull();
        var accessRules = new AccessRules(dbContext, new TestCurrentUser(memberId));
        (await accessRules.CanRead(SharedResourceType.Account, resourceId, CancellationToken.None))
            .Should().BeTrue();
        (await accessRules.CanEdit(SharedResourceType.Account, resourceId, CancellationToken.None))
            .Should().Be(permission == GrantPermission.Editor);
    }

    [Test]
    public async Task Invalid_grant_values_are_rejected_without_writes()
    {
        var valid = ValidCreate();
        var invalid = new[]
        {
            valid with { ResourceType = (SharedResourceType)999 },
            valid with { ResourceId = Guid.Empty },
            valid with { Permission = (GrantPermission)999 },
            valid with { KeyEnvelopeReference = Guid.Empty }
        };

        foreach (var request in invalid)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().ResourceGrants.CountAsync())
            .Should().Be(0);
    }

    [Test]
    public async Task Unauthorized_inactive_deleted_missing_type_mismatch_and_cross_group_create_are_neutral()
    {
        using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        var nonowner = await memberClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var missing = await client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.CreateVersion7()}/grants",
            ValidCreate());
        var typeMismatch = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate() with { ResourceType = SharedResourceType.Budget });
        var crossGroup = await client.PostAsJsonAsync(
            $"/api/v1/groups/{otherGroupId}/grants",
            ValidCreate());
        await SetMembershipState(ownerId, MembershipState.Revoked);
        var inactive = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        await SetMembershipState(ownerId, MembershipState.Active);
        await SetGroupDeleted(groupId);
        var deleted = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());

        await AssertIdenticalNotFound(nonowner, missing, typeMismatch, crossGroup, inactive, deleted);
    }

    [Test]
    public async Task Sequential_and_concurrent_duplicate_grants_return_a_stable_conflict()
    {
        var first = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", ValidCreate());
        var sequential = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", ValidCreate());

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        sequential.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await sequential.Content.ReadAsStringAsync()).Should().Contain("ResourceGrantAlreadyActive");

        var anotherResourceId = Guid.CreateVersion7();
        var request = ValidCreate() with { ResourceId = anotherResourceId };
        var concurrent = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", request),
            client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", request));

        concurrent.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Created);
        concurrent.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Conflict);
        (await concurrent.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .Content.ReadAsStringAsync()).Should().Contain("ResourceGrantAlreadyActive");
    }

    [Test]
    public async Task Concurrent_first_claim_by_two_users_has_one_owner_and_no_half_grant()
    {
        var unclaimedResourceId = Guid.CreateVersion7();
        var request = ValidCreate() with { ResourceId = unclaimedResourceId };
        using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", request),
            memberClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/grants", request));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Created);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var resource = await dbContext.SharedResources.SingleAsync(item => item.Id == unclaimedResourceId);
        new[] { ownerId, memberId }.Should().Contain(resource.OwnerUserId);
        var grant = await dbContext.ResourceGrants.SingleAsync(item => item.ResourceId == unclaimedResourceId);
        grant.GrantedByUserId.Should().Be(resource.OwnerUserId);
        grant.State.Should().Be(GrantState.Active);
        resource.Type.Should().Be(SharedResourceType.Account);
        (await dbContext.SharedResources.CountAsync(item => item.Id == unclaimedResourceId)).Should().Be(1);
        (await dbContext.ResourceGrants.CountAsync(item => item.ResourceId == unclaimedResourceId)).Should().Be(1);
    }

    [Test]
    public async Task Listing_returns_only_active_same_group_grants_to_active_members()
    {
        var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<ResourceGrantContract>())!;
        await SeedGrant(otherGroupId, resourceId, GrantPermission.Editor, GrantState.Active);
        var revokedId = await SeedGrant(groupId, Guid.CreateVersion7(), GrantPermission.Viewer, GrantState.Revoked);
        using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();

        var response = await memberClient.GetAsync($"/api/v1/groups/{groupId}/grants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var grants = await response.Content.ReadFromJsonAsync<ResourceGrantContract[]>();
        grants.Should().ContainSingle(grant => grant.Id == created.Id);
        grants.Should().NotContain(grant => grant.GroupId == otherGroupId || grant.Id == revokedId);
        await SetMembershipState(memberId, MembershipState.Revoked);
        var inactive = await memberClient.GetAsync($"/api/v1/groups/{groupId}/grants");
        inactive.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SetMembershipState(memberId, MembershipState.Active);
        using var unrelatedFactory = new WebApiTestFactory(connectionString).AsUser(unrelatedId);
        using var unrelatedClient = unrelatedFactory.CreateClient();
        var unrelated = await unrelatedClient.GetAsync($"/api/v1/groups/{groupId}/grants");
        var missing = await client.GetAsync($"/api/v1/groups/{Guid.CreateVersion7()}/grants");
        await SetGroupDeleted(groupId);
        var deleted = await memberClient.GetAsync($"/api/v1/groups/{groupId}/grants");
        await AssertIdenticalNotFound(inactive, unrelated, missing, deleted);
    }

    [Test]
    public async Task List_final_query_rechecks_membership_before_returning_grants()
    {
        (await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate())).StatusCode.Should().Be(HttpStatusCode.Created);
        var listRaceInterceptor = new ResourceGrantListRaceInterceptor(connectionString);
        await using var raceFactory = new WebApiTestFactory(connectionString, listRaceInterceptor)
            .AsUser(memberId);
        using var raceClient = raceFactory.CreateClient();
        listRaceInterceptor.Arm(groupId, memberId);

        var response = await raceClient.GetAsync($"/api/v1/groups/{groupId}/grants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ResourceGrantContract[]>()).Should().BeEmpty();
    }

    [Test]
    public async Task Revoking_a_grant_immediately_blocks_member_access_and_preserves_owner_access()
    {
        var create = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var grant = (await create.Content.ReadFromJsonAsync<ResourceGrantContract>())!;
        using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        (await memberClient.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes"))!
            .Records.Should().Contain(record => record.Id == recordId);

        var response = await client.DeleteAsync(
            $"/api/v1/groups/{groupId}/grants/{grant.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotation = await response.Content.ReadFromJsonAsync<KeyRotationContract>();
        rotation!.KeyRotationRequired.Should().BeTrue();
        rotation.Warning.Should().Be("Revocation cannot erase data that a former member already saved.");
        (await memberClient.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes"))!
            .Records.Should().NotContain(record => record.Id == recordId);
        (await client.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes"))!
            .Records.Should().Contain(record => record.Id == recordId);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var stored = await dbContext.ResourceGrants.SingleAsync(item => item.Id == grant.Id);
        stored.State.Should().Be(GrantState.Revoked);
        stored.KeyEnvelopeReference.Should().BeNull();
        stored.RevokedAt.Should().Be(stored.UpdatedAt);
        var accessRules = new AccessRules(dbContext, new TestCurrentUser(memberId));
        (await accessRules.CanRead(SharedResourceType.Account, resourceId, CancellationToken.None))
            .Should().BeFalse();
    }

    [Test]
    public async Task Revoking_requires_the_resource_owner_and_exact_group_and_active_grant()
    {
        var create = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var grant = (await create.Content.ReadFromJsonAsync<ResourceGrantContract>())!;
        using var memberFactory = new WebApiTestFactory(connectionString).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();

        var nonowner = await memberClient.DeleteAsync($"/api/v1/groups/{groupId}/grants/{grant.Id}");
        var crossGroup = await client.DeleteAsync($"/api/v1/groups/{otherGroupId}/grants/{grant.Id}");
        var missing = await client.DeleteAsync($"/api/v1/groups/{groupId}/grants/{Guid.CreateVersion7()}");
        await SetGroupDeleted(groupId);
        var deleted = await client.DeleteAsync($"/api/v1/groups/{groupId}/grants/{grant.Id}");

        await AssertIdenticalNotFound(nonowner, crossGroup, missing, deleted);
    }

    [Test]
    public async Task Grant_create_and_revoke_persistence_failures_are_atomic()
    {
        var unclaimedResourceId = Guid.CreateVersion7();
        await using var failingCreateFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<ResourceGrant>()).AsUser(ownerId);
        using var failingCreateClient = failingCreateFactory.CreateClient();

        var failedCreate = await failingCreateClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate() with { ResourceId = unclaimedResourceId });

        failedCreate.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using (var verifyScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            (await dbContext.SharedResources.AnyAsync(item => item.Id == unclaimedResourceId)).Should().BeFalse();
            (await dbContext.ResourceGrants.AnyAsync(item => item.ResourceId == unclaimedResourceId)).Should().BeFalse();
        }

        var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<ResourceGrantContract>())!;
        await using var failingRevokeFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<ResourceGrant>()).AsUser(ownerId);
        using var failingRevokeClient = failingRevokeFactory.CreateClient();

        var failedRevoke = await failingRevokeClient.DeleteAsync(
            $"/api/v1/groups/{groupId}/grants/{created.Id}");

        failedRevoke.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var stored = await finalScope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ResourceGrants.SingleAsync(item => item.Id == created.Id);
        stored.State.Should().Be(GrantState.Active);
        stored.KeyEnvelopeReference.Should().Be(created.KeyEnvelopeReference);
    }

    [Test]
    public async Task Management_create_racing_sync_add_has_one_active_grant_and_no_unique_failure()
    {
        await using var connection = await HoldResourceLock(resourceId);
        var attempts = resourceLockInterceptor.WaitForAttempts(2);
        var managementTask = client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var syncTask = client.PostAsJsonAsync(
            $"/api/v1/sync/records/{recordId}/envelopes",
            ValidEnvelope());
        await attempts.WaitAsync(TimeSpan.FromSeconds(5));

        await connection.Transaction.CommitAsync();
        await Task.WhenAll(managementTask, syncTask);

        syncTask.Result.StatusCode.Should().Be(HttpStatusCode.OK);
        managementTask.Result.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var grant = await dbContext.ResourceGrants.SingleAsync(item =>
            item.GroupId == groupId && item.ResourceId == resourceId && item.State == GrantState.Active);
        var envelope = await dbContext.RecordEnvelopes.SingleAsync(item =>
            item.EncryptedRecordId == recordId && item.GroupId == groupId);
        grant.KeyEnvelopeReference.Should().Be(envelope.Id);
        (await dbContext.ResourceGrants.CountAsync(item =>
            item.GroupId == groupId && item.ResourceId == resourceId && item.State == GrantState.Active))
            .Should().Be(1);
    }

    [Test]
    public async Task Grant_first_claim_racing_root_record_creation_has_one_owner_and_a_neutral_loser()
    {
        var unclaimedResourceId = Guid.CreateVersion7();
        using var memberFactory = new WebApiTestFactory(connectionString, resourceLockInterceptor).AsUser(memberId);
        using var memberClient = memberFactory.CreateClient();
        await using var connection = await HoldResourceLock(unclaimedResourceId);
        var attempts = resourceLockInterceptor.WaitForAttempts(2);
        var grantTask = client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate() with { ResourceId = unclaimedResourceId });
        var syncTask = memberClient.PostAsJsonAsync(
            "/api/v1/sync/records",
            RootCreate(unclaimedResourceId));
        await attempts.WaitAsync(TimeSpan.FromSeconds(5));

        await connection.Transaction.CommitAsync();
        await Task.WhenAll(grantTask, syncTask);

        new[] { grantTask.Result, syncTask.Result }
            .Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Created);
        new[] { grantTask.Result, syncTask.Result }
            .Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NotFound);
        new[] { grantTask.Result, syncTask.Result }
            .Should().NotContain(response => response.StatusCode == HttpStatusCode.InternalServerError);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var resource = await dbContext.SharedResources.SingleAsync(item => item.Id == unclaimedResourceId);
        if (resource.OwnerUserId == ownerId)
        {
            (await dbContext.ResourceGrants.CountAsync(item => item.ResourceId == unclaimedResourceId))
                .Should().Be(1);
            (await dbContext.EncryptedRecords.AnyAsync(item => item.Id == unclaimedResourceId))
                .Should().BeFalse();
        }
        else
        {
            resource.OwnerUserId.Should().Be(memberId);
            (await dbContext.ResourceGrants.AnyAsync(item => item.ResourceId == unclaimedResourceId))
                .Should().BeFalse();
            (await dbContext.EncryptedRecords.CountAsync(item => item.Id == unclaimedResourceId))
                .Should().Be(1);
            (await dbContext.RecordEnvelopes.CountAsync(item => item.EncryptedRecordId == unclaimedResourceId))
                .Should().Be(1);
            (await dbContext.SyncOperations.CountAsync(item => item.EncryptedRecordId == unclaimedResourceId))
                .Should().Be(1);
        }
    }

    [Test]
    public async Task Management_revoke_racing_sync_add_leaves_grant_state_and_reference_consistent()
    {
        var createdResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/grants",
            ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<ResourceGrantContract>())!;
        await using var connection = await HoldResourceLock(resourceId);
        var attempts = resourceLockInterceptor.WaitForAttempts(2);
        var revokeTask = client.DeleteAsync($"/api/v1/groups/{groupId}/grants/{created.Id}");
        var syncTask = client.PostAsJsonAsync(
            $"/api/v1/sync/records/{recordId}/envelopes",
            ValidEnvelope());
        await attempts.WaitAsync(TimeSpan.FromSeconds(5));

        await connection.Transaction.CommitAsync();
        await Task.WhenAll(revokeTask, syncTask);

        revokeTask.Result.StatusCode.Should().Be(HttpStatusCode.OK);
        syncTask.Result.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var grant = await dbContext.ResourceGrants.SingleAsync(item => item.Id == created.Id);
        if (grant.State == GrantState.Active)
        {
            var envelope = await dbContext.RecordEnvelopes.SingleAsync(item =>
                item.EncryptedRecordId == recordId && item.GroupId == groupId);
            grant.KeyEnvelopeReference.Should().Be(envelope.Id);
            grant.RevokedAt.Should().BeNull();
        }
        else
        {
            grant.State.Should().Be(GrantState.Revoked);
            grant.KeyEnvelopeReference.Should().BeNull();
            grant.RevokedAt.Should().NotBeNull();
        }
    }

    private async Task Seed()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.Users.AddRange(
            User(ownerId, now),
            User(memberId, now),
            User(unrelatedId, now));
        dbContext.Groups.AddRange(
            Group(groupId, ownerId, now),
            Group(otherGroupId, unrelatedId, now));
        dbContext.GroupMemberships.AddRange(
            Membership(groupId, ownerId, MembershipRole.Owner, now),
            Membership(groupId, memberId, MembershipRole.Member, now),
            Membership(otherGroupId, unrelatedId, MembershipRole.Owner, now));
        dbContext.SharedResources.Add(new SharedResource
        {
            Id = resourceId,
            Type = SharedResourceType.Account,
            OwnerUserId = ownerId,
            CreatedAt = now
        });
        dbContext.EncryptedRecords.Add(new EncryptedRecord
        {
            Id = recordId,
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerId,
            ParentResourceId = resourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1],
            Ciphertext = [2],
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private CreateGrantRequest ValidCreate() => new(
        SharedResourceType.Account,
        resourceId,
        GrantPermission.Viewer,
        Guid.CreateVersion7());

    private AddEnvelopeRequest ValidEnvelope() => new(
        groupId,
        GrantPermission.Editor,
        [31],
        [32],
        [33],
        1);

    private static CreateSyncRequest RootCreate(Guid value) => new(
        [new CreateSyncRecordRequest(
            value,
            $"root-{value:N}",
            EncryptedRecordType.Account,
            value,
            1,
            [41],
            [42],
            new PersonalEnvelopeRequest([43], [44], 1))]);

    private async Task<HeldResourceLock> HoldResourceLock(Guid value)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", $"resource:{value:N}");
        await command.ExecuteNonQueryAsync();
        return new HeldResourceLock(connection, transaction);
    }

    private async Task SetMembershipState(Guid userId, MembershipState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var membership = await dbContext.GroupMemberships.SingleAsync(item =>
            item.GroupId == groupId && item.UserId == userId);
        membership.State = state;
        await dbContext.SaveChangesAsync();
    }

    private async Task SetGroupDeleted(Guid value)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.SingleAsync(item => item.Id == value);
        group.MarkAsDeleted();
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedGrant(
        Guid value,
        Guid targetResourceId,
        GrantPermission permission,
        GrantState state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        if (!await dbContext.SharedResources.AnyAsync(item => item.Id == targetResourceId))
        {
            dbContext.SharedResources.Add(new SharedResource
            {
                Id = targetResourceId,
                Type = SharedResourceType.Account,
                OwnerUserId = ownerId,
                CreatedAt = now
            });
        }
        var grant = new ResourceGrant
        {
            Id = Guid.CreateVersion7(),
            GroupId = value,
            ResourceType = SharedResourceType.Account,
            ResourceId = targetResourceId,
            Permission = permission,
            State = state,
            GrantedByUserId = ownerId,
            KeyEnvelopeReference = state == GrantState.Active ? Guid.CreateVersion7() : null,
            CreatedAt = now,
            UpdatedAt = now,
            RevokedAt = state == GrantState.Revoked ? now : null
        };
        dbContext.ResourceGrants.Add(grant);
        await dbContext.SaveChangesAsync();
        return grant.Id;
    }

    private static async Task AssertIdenticalNotFound(params HttpResponseMessage[] responses)
    {
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsByteArrayAsync()));
        bodies.Should().OnlyContain(body => body.SequenceEqual(bodies[0]));
    }

    private static XpenseUser User(Guid id, DateTime now) => new()
    {
        Id = id,
        State = AccountState.Active,
        CreatedAt = now
    };

    private static Group Group(Guid id, Guid ownerUserId, DateTime now) => new()
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
        Guid value,
        Guid userId,
        MembershipRole role,
        DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = value,
        UserId = userId,
        Role = role,
        State = MembershipState.Active,
        GroupKeyEnvelope = [3],
        EnvelopeProtocolVersion = 1,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed record CreateGrantRequest(
        SharedResourceType ResourceType,
        Guid ResourceId,
        GrantPermission Permission,
        Guid? KeyEnvelopeReference);

    private sealed record ResourceGrantContract(
        Guid Id,
        Guid GroupId,
        SharedResourceType ResourceType,
        Guid ResourceId,
        GrantPermission Permission,
        GrantState State,
        Guid GrantedByUserId,
        Guid? KeyEnvelopeReference,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? RevokedAt);

    private sealed record KeyRotationContract(bool KeyRotationRequired, string Warning);

    private sealed record AddEnvelopeRequest(
        Guid GroupId,
        GrantPermission Permission,
        byte[] WrappedKey,
        byte[] Nonce,
        byte[]? EncapsulatedKey,
        int ProtocolVersion);

    private sealed record CreateSyncRequest(CreateSyncRecordRequest[] Records);

    private sealed record CreateSyncRecordRequest(
        Guid Id,
        string IdempotencyKey,
        EncryptedRecordType RecordType,
        Guid? ParentResourceId,
        int ProtocolVersion,
        byte[] Nonce,
        byte[] Ciphertext,
        PersonalEnvelopeRequest PersonalEnvelope);

    private sealed record PersonalEnvelopeRequest(
        byte[] WrappedKey,
        byte[] Nonce,
        int ProtocolVersion);

    private sealed record SyncChangesContract(SyncRecordContract[] Records);

    private sealed record SyncRecordContract(Guid Id);

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id { get; } = id;

        public bool IsAuthenticated => true;
    }

    private sealed class HeldResourceLock : IAsyncDisposable
    {
        private readonly NpgsqlConnection connection;

        public HeldResourceLock(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            this.connection = connection;
            Transaction = transaction;
        }

        public NpgsqlTransaction Transaction { get; }

        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class ResourceLockAttemptInterceptor : DbCommandInterceptor
    {
        private TaskCompletionSource? expectedAttempts;
        private int remainingAttempts;

        public Task WaitForAttempts(int count)
        {
            remainingAttempts = count;
            expectedAttempts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return expectedAttempts.Task;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) &&
                expectedAttempts is not null &&
                Interlocked.Decrement(ref remainingAttempts) == 0)
                expectedAttempts.TrySetResult();

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ResourceGrantListRaceInterceptor(string value) : DbCommandInterceptor
    {
        private Guid groupId;
        private Guid userId;
        private int armed;

        public void Arm(Guid targetGroupId, Guid targetUserId)
        {
            groupId = targetGroupId;
            userId = targetUserId;
            Interlocked.Exchange(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1 &&
                command.CommandText.Contains("ResourceGrants", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref armed, 0) == 1)
            {
                await using var connection = new NpgsqlConnection(value);
                await connection.OpenAsync(cancellationToken);
                await using var update = new NpgsqlCommand(
                    "UPDATE \"Xpense\".\"GroupMemberships\" SET \"State\" = 2, \"RevokedAt\" = NOW(), \"UpdatedAt\" = NOW() WHERE \"GroupId\" = @groupId AND \"UserId\" = @userId",
                    connection);
                update.Parameters.AddWithValue("groupId", groupId);
                update.Parameters.AddWithValue("userId", userId);
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
