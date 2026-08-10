using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class GroupEndpointTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private GroupLockAttemptInterceptor lockInterceptor = null!;
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        connectionString = await PostgresFixture.CreateDatabase();
        lockInterceptor = new GroupLockAttemptInterceptor();
        factory = new WebApiTestFactory(connectionString, lockInterceptor);
        client = await factory.CreateAuthenticatedClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task Creating_a_group_makes_the_creator_its_owner_with_an_active_membership()
    {
        var userId = await CurrentUserId(client);
        var request = ValidCreate();

        var response = await client.PostAsJsonAsync("/api/v1/groups", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeTrue();
        response.Headers.Location.AbsolutePath.Should().MatchRegex("^/api/v1/groups/[0-9a-f-]+$");
        var created = await response.Content.ReadFromJsonAsync<GroupContract>();
        created.Should().NotBeNull();
        created!.OwnerUserId.Should().Be(userId);
        created.Role.Should().Be(MembershipRole.Owner);
        created.NameCiphertext.Should().Be(request.NameCiphertext);
        created.NameNonce.Should().Be(request.NameNonce);
        created.ProtocolVersion.Should().Be(1);
        created.GroupKeyEnvelope.Should().Be(request.OwnerKeyEnvelope);
        created.EnvelopeProtocolVersion.Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.SingleAsync(group => group.Id == created.Id);
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == created.Id && membership.UserId == userId);
        group.OwnerUserId.Should().Be(userId);
        group.NameCiphertext.Should().Equal(Convert.FromBase64String(request.NameCiphertext!));
        group.NameNonce.Should().Equal(Convert.FromBase64String(request.NameNonce!));
        group.ProtocolVersion.Should().Be(1);
        group.IsDeleted.Should().BeFalse();
        membership.Role.Should().Be(MembershipRole.Owner);
        membership.State.Should().Be(MembershipState.Active);
        membership.GroupKeyEnvelope.Should().Equal(Convert.FromBase64String(request.OwnerKeyEnvelope!));
        membership.EnvelopeProtocolVersion.Should().Be(1);
    }

    [Test]
    public async Task Creating_a_group_rejects_every_invalid_encrypted_value_without_writes()
    {
        var valid = ValidCreate();
        var cases = new[]
        {
            valid with { NameCiphertext = null },
            valid with { NameCiphertext = string.Empty },
            valid with { NameCiphertext = "not-base64" },
            valid with { NameCiphertext = "AR==" },
            valid with { NameCiphertext = Convert.ToBase64String(new byte[4097]) },
            valid with { NameNonce = null },
            valid with { NameNonce = string.Empty },
            valid with { NameNonce = "not-base64" },
            valid with { NameNonce = Convert.ToBase64String(new byte[4097]) },
            valid with { OwnerKeyEnvelope = null },
            valid with { OwnerKeyEnvelope = string.Empty },
            valid with { OwnerKeyEnvelope = "not-base64" },
            valid with { OwnerKeyEnvelope = Convert.ToBase64String(new byte[4097]) },
            valid with { ProtocolVersion = 0 },
            valid with { ProtocolVersion = 2 }
        };

        foreach (var invalid in cases)
        {
            var response = await client.PostAsJsonAsync("/api/v1/groups", invalid);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.Groups.CountAsync()).Should().Be(0);
        (await dbContext.GroupMemberships.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task A_create_persistence_failure_leaves_neither_group_nor_owner_membership()
    {
        await using var failingFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<Group>());
        using var failingClient = await failingFactory.CreateAuthenticatedClient();

        var response = await failingClient.PostAsJsonAsync("/api/v1/groups", ValidCreate());

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var scope = failingFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.Groups.CountAsync()).Should().Be(0);
        (await dbContext.GroupMemberships.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Listing_groups_returns_only_the_callers_active_memberships_in_nondeleted_groups()
    {
        var callerId = await CurrentUserId(client);
        using var otherClient = await factory.CreateAuthenticatedClient();
        var otherId = await CurrentUserId(otherClient);
        var first = await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Active, false, [11]);
        var second = await SeedGroup(callerId, callerId, MembershipRole.Owner, MembershipState.Active, false, [12]);
        await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Revoked, false, [13]);
        await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Active, true, [14]);
        await SeedGroup(otherId, otherId, MembershipRole.Owner, MembershipState.Active, false, [15]);

        var response = await client.GetAsync("/api/v1/groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var groups = await response.Content.ReadFromJsonAsync<GroupContract[]>();
        groups.Should().NotBeNull();
        var returnedGroups = groups ?? throw new InvalidOperationException("The group list was empty");
        returnedGroups.Select(group => group.Id).Should().Equal(first, second);
        returnedGroups[0].Role.Should().Be(MembershipRole.Member);
        returnedGroups[0].GroupKeyEnvelope.Should().Be(Convert.ToBase64String([11]));
        returnedGroups[1].Role.Should().Be(MembershipRole.Owner);
        returnedGroups[1].GroupKeyEnvelope.Should().Be(Convert.ToBase64String([12]));
    }

    [Test]
    public async Task Getting_a_group_requires_the_callers_active_membership_and_hides_every_other_state()
    {
        var callerId = await CurrentUserId(client);
        using var otherClient = await factory.CreateAuthenticatedClient();
        var otherId = await CurrentUserId(otherClient);
        var activeId = await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Active, false, [31]);
        var revokedId = await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Revoked, false, [32]);
        var deletedId = await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Active, true, [33]);
        var unrelatedId = await SeedGroup(otherId, otherId, MembershipRole.Owner, MembershipState.Active, false, [34]);

        var allowed = await client.GetAsync($"/api/v1/groups/{activeId}");
        var revoked = await client.GetAsync($"/api/v1/groups/{revokedId}");
        var deleted = await client.GetAsync($"/api/v1/groups/{deletedId}");
        var unrelated = await client.GetAsync($"/api/v1/groups/{unrelatedId}");
        var missing = await client.GetAsync($"/api/v1/groups/{Guid.CreateVersion7()}");

        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        var group = await allowed.Content.ReadFromJsonAsync<GroupContract>();
        group.Should().NotBeNull();
        group!.Id.Should().Be(activeId);
        group.Role.Should().Be(MembershipRole.Member);
        group.GroupKeyEnvelope.Should().Be(Convert.ToBase64String([31]));
        new[] { revoked, deleted, unrelated, missing }.Should()
            .OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        var hiddenBodies = await Task.WhenAll(new[] { revoked, deleted, unrelated, missing }
            .Select(response => response.Content.ReadAsByteArrayAsync()));
        hiddenBodies.Should().OnlyContain(body => body.SequenceEqual(hiddenBodies[0]));
    }

    [Test]
    public async Task An_active_member_without_an_envelope_can_list_and_get_the_group_without_an_envelope_leak()
    {
        var callerId = await CurrentUserId(client);
        using var otherClient = await factory.CreateAuthenticatedClient();
        var otherId = await CurrentUserId(otherClient);
        var groupId = await SeedGroup(otherId, callerId, MembershipRole.Member, MembershipState.Active, false, null);
        await AddMembership(groupId, otherId, MembershipState.Active, [101, 102]);

        var list = await client.GetFromJsonAsync<GroupContract[]>("/api/v1/groups");
        var get = await client.GetFromJsonAsync<GroupContract>($"/api/v1/groups/{groupId}");

        list.Should().ContainSingle();
        list![0].GroupKeyEnvelope.Should().BeNull();
        get.Should().NotBeNull();
        get!.GroupKeyEnvelope.Should().BeNull();
    }

    [Test]
    public async Task Updating_a_group_as_owner_changes_only_the_encrypted_name_and_timestamp()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var created = await createdResponse.Content.ReadFromJsonAsync<GroupContract>();
        var groupId = created!.Id;
        Group before;
        GroupMembership membershipBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            before = await dbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
            membershipBefore = await dbContext.GroupMemberships.AsNoTracking()
                .SingleAsync(membership => membership.GroupId == groupId);
        }
        await Task.Delay(10);
        var request = new UpdateRequest(
            Convert.ToBase64String([41, 42]),
            Convert.ToBase64String([43, 44]),
            1);

        var response = await client.PutAsJsonAsync($"/api/v1/groups/{groupId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedResponse = await response.Content.ReadFromJsonAsync<GroupContract>();
        updatedResponse!.NameCiphertext.Should().Be(request.NameCiphertext);
        updatedResponse.NameNonce.Should().Be(request.NameNonce);
        updatedResponse.GroupKeyEnvelope.Should().Be(created.GroupKeyEnvelope);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var updated = await verifyDbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
        var membershipAfter = await verifyDbContext.GroupMemberships.AsNoTracking()
            .SingleAsync(membership => membership.GroupId == groupId);
        updated.OwnerUserId.Should().Be(before.OwnerUserId);
        updated.CreatedAt.Should().Be(before.CreatedAt);
        updated.UpdatedAt.Should().BeAfter(before.UpdatedAt);
        updated.NameCiphertext.Should().Equal([41, 42]);
        updated.NameNonce.Should().Equal([43, 44]);
        updated.ProtocolVersion.Should().Be(1);
        membershipAfter.Should().BeEquivalentTo(membershipBefore);
    }

    [Test]
    public async Task Updating_as_an_active_owner_without_an_envelope_returns_null_without_another_members_envelope()
    {
        var ownerId = await CurrentUserId(client);
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var groupId = await SeedGroup(ownerId, ownerId, MembershipRole.Owner, MembershipState.Active, false, null);
        await AddMembership(groupId, memberId, MembershipState.Active, [103, 104]);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}",
            new UpdateRequest(Convert.ToBase64String([105]), Convert.ToBase64String([106]), 1));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<GroupContract>();
        updated.Should().NotBeNull();
        updated!.GroupKeyEnvelope.Should().BeNull();
    }

    [Test]
    public async Task Updating_a_group_rejects_every_invalid_encrypted_value_without_changes()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!;
        var valid = new UpdateRequest(
            Convert.ToBase64String([81]),
            Convert.ToBase64String([82]),
            1);
        var cases = new[]
        {
            valid with { NameCiphertext = null },
            valid with { NameCiphertext = string.Empty },
            valid with { NameCiphertext = "not-base64" },
            valid with { NameCiphertext = "AR==" },
            valid with { NameCiphertext = Convert.ToBase64String(new byte[4097]) },
            valid with { NameNonce = null },
            valid with { NameNonce = string.Empty },
            valid with { NameNonce = "not-base64" },
            valid with { NameNonce = Convert.ToBase64String(new byte[4097]) },
            valid with { ProtocolVersion = 0 },
            valid with { ProtocolVersion = 2 }
        };

        foreach (var invalid in cases)
        {
            var response = await client.PutAsJsonAsync($"/api/v1/groups/{created.Id}", invalid);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var group = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Groups
            .SingleAsync(group => group.Id == created.Id);
        group.NameCiphertext.Should().Equal(Convert.FromBase64String(created.NameCiphertext));
        group.NameNonce.Should().Equal(Convert.FromBase64String(created.NameNonce));
        group.UpdatedAt.Should().Be(created.UpdatedAt);
    }

    [Test]
    public async Task Deleting_an_owner_only_group_soft_deletes_it_and_preserves_its_history()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var created = await createdResponse.Content.ReadFromJsonAsync<GroupContract>();
        var groupId = created!.Id;

        var response = await client.DeleteAsync($"/api/v1/groups/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<GroupContract[]>("/api/v1/groups")).Should().BeEmpty();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.IgnoreQueryFilters().SingleAsync(group => group.Id == groupId);
        group.IsDeleted.Should().BeTrue();
        group.UpdatedAt.Should().BeAfter(group.CreatedAt);
        (await dbContext.GroupMemberships.CountAsync(membership => membership.GroupId == groupId)).Should().Be(1);
    }

    [Test]
    public async Task Deleting_a_group_with_another_active_member_is_refused()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        await AddMembership(groupId, memberId, MembershipState.Active);

        var response = await client.DeleteAsync($"/api/v1/groups/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var group = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Groups
            .SingleAsync(group => group.Id == groupId);
        group.IsDeleted.Should().BeFalse();
    }

    [TestCase(MembershipState.AwaitingOwnerApproval)]
    [TestCase(MembershipState.Revoked)]
    public async Task Awaiting_and_revoked_memberships_do_not_block_group_deletion(MembershipState state)
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        await AddMembership(groupId, memberId, state);

        var response = await client.DeleteAsync($"/api/v1/groups/{groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Member_unrelated_and_missing_group_mutations_return_byte_identical_not_found_responses()
    {
        var ownerId = await CurrentUserId(client);
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        using var unrelatedClient = await factory.CreateAuthenticatedClient();
        var groupId = await SeedGroup(ownerId, memberId, MembershipRole.Member, MembershipState.Active, false, [51]);
        var missingId = Guid.CreateVersion7();
        var update = new UpdateRequest(Convert.ToBase64String([52]), Convert.ToBase64String([53]), 1);

        var memberUpdate = await memberClient.PutAsJsonAsync($"/api/v1/groups/{groupId}", update);
        var unrelatedUpdate = await unrelatedClient.PutAsJsonAsync($"/api/v1/groups/{groupId}", update);
        var missingUpdate = await client.PutAsJsonAsync($"/api/v1/groups/{missingId}", update);
        var memberDelete = await memberClient.DeleteAsync($"/api/v1/groups/{groupId}");
        var unrelatedDelete = await unrelatedClient.DeleteAsync($"/api/v1/groups/{groupId}");
        var missingDelete = await client.DeleteAsync($"/api/v1/groups/{missingId}");

        await AssertIdenticalNotFound(memberUpdate, unrelatedUpdate, missingUpdate);
        await AssertIdenticalNotFound(memberDelete, unrelatedDelete, missingDelete);
    }

    [Test]
    public async Task A_waiting_update_observes_a_group_delete_committed_under_the_same_lock()
    {
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("key", created.Id.ToString("N"));
            await lockCommand.ExecuteNonQueryAsync();
        }
        await using (var deleteCommand = new NpgsqlCommand(
            "UPDATE \"Xpense\".\"Groups\" SET \"IsDeleted\" = TRUE, \"UpdatedAt\" = NOW() WHERE \"Id\" = @id",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("id", created.Id);
            await deleteCommand.ExecuteNonQueryAsync();
        }
        var lockAttempt = lockInterceptor.WaitForNextAttempt();
        var updateTask = client.PutAsJsonAsync(
            $"/api/v1/groups/{created.Id}",
            new UpdateRequest(Convert.ToBase64String([61]), Convert.ToBase64String([62]), 1));
        await lockAttempt.WaitAsync(TimeSpan.FromSeconds(5));

        await transaction.CommitAsync();
        var response = await updateTask;

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var group = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Groups
            .SingleAsync(group => group.Id == created.Id);
        group.IsDeleted.Should().BeTrue();
        group.NameCiphertext.Should().Equal(Convert.FromBase64String(created.NameCiphertext));
    }

    [Test]
    public async Task Cancellation_after_the_main_commit_cannot_turn_a_committed_update_into_a_failure()
    {
        var cancellationInterceptor = new CancelLockCommitAfterMainCommitInterceptor();
        await using var cancellationFactory = new WebApiTestFactory(connectionString, cancellationInterceptor);
        using var cancellationClient = await cancellationFactory.CreateAuthenticatedClient();
        var createdResponse = await cancellationClient.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var created = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!;

        var response = await cancellationClient.PutAsJsonAsync(
            $"/api/v1/groups/{created.Id}",
            new UpdateRequest(Convert.ToBase64String([111]), Convert.ToBase64String([112]), 1));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = cancellationFactory.Services.CreateAsyncScope();
        var group = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().Groups
            .SingleAsync(group => group.Id == created.Id);
        group.NameCiphertext.Should().Equal([111]);
    }

    [Test]
    public async Task Every_group_route_requires_authentication()
    {
        using var anonymous = factory.CreateClient();
        var groupId = Guid.CreateVersion7();

        var responses = new[]
        {
            await anonymous.PostAsJsonAsync("/api/v1/groups", ValidCreate()),
            await anonymous.GetAsync("/api/v1/groups"),
            await anonymous.GetAsync($"/api/v1/groups/{groupId}"),
            await anonymous.PutAsJsonAsync(
                $"/api/v1/groups/{groupId}",
                new UpdateRequest(Convert.ToBase64String([91]), Convert.ToBase64String([92]), 1)),
            await anonymous.DeleteAsync($"/api/v1/groups/{groupId}")
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Active_members_see_the_active_roster_emails_roles_and_envelope_availability_without_envelope_bytes()
    {
        var owner = await client.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await memberClient.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        using var revokedClient = await factory.CreateAuthenticatedClient();
        var revoked = await revokedClient.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, member!.Id, MembershipState.Active, [121, 122]);
        await AddMembership(groupId, revoked!.Id, MembershipState.Revoked, [123, 124]);

        var response = await memberClient.GetAsync($"/api/v1/groups/{groupId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var members = await response.Content.ReadFromJsonAsync<GroupMemberContract[]>();
        members.Should().NotBeNull();
        members!.Should().BeEquivalentTo([
            new GroupMemberContract(owner!.Id, owner.Email, MembershipRole.Owner, true),
            new GroupMemberContract(member.Id, member.Email, MembershipRole.Member, true)
        ]);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("groupKeyEnvelope");
        body.Should().NotContain(Convert.ToBase64String([7, 8, 9]));
        body.Should().NotContain(Convert.ToBase64String([121, 122]));
        body.Should().NotContain(revoked.Email);
    }

    [TestCase(RosterRaceMode.RevokeCaller)]
    [TestCase(RosterRaceMode.DeleteGroup)]
    public async Task Roster_query_rechecks_access_after_the_initial_authorization_query(RosterRaceMode mode)
    {
        var raceInterceptor = new RosterRaceInterceptor(connectionString, mode);
        await using var raceFactory = new WebApiTestFactory(connectionString, raceInterceptor);
        using var ownerClient = await raceFactory.CreateAuthenticatedClient();
        var owner = await ownerClient.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        using var memberClient = await raceFactory.CreateAuthenticatedClient();
        var member = await memberClient.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        var createdResponse = await ownerClient.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, member!.Id, MembershipState.Active, [125, 126]);
        raceInterceptor.Arm(groupId, member.Id);

        var response = await memberClient.GetAsync($"/api/v1/groups/{groupId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<GroupMemberContract[]>()).Should().BeEmpty();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(owner!.Email);
        body.Should().NotContain(member.Email);
    }

    [Test]
    public async Task Revoked_unrelated_deleted_and_missing_roster_callers_receive_identical_not_found()
    {
        using var revokedClient = await factory.CreateAuthenticatedClient();
        var revokedId = await CurrentUserId(revokedClient);
        using var unrelatedClient = await factory.CreateAuthenticatedClient();
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, revokedId, MembershipState.Revoked);

        var revoked = await revokedClient.GetAsync($"/api/v1/groups/{groupId}/members");
        var unrelated = await unrelatedClient.GetAsync($"/api/v1/groups/{groupId}/members");
        var missing = await client.GetAsync($"/api/v1/groups/{Guid.CreateVersion7()}/members");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var group = await dbContext.Groups.SingleAsync(group => group.Id == groupId);
            group.MarkAsDeleted();
            await dbContext.SaveChangesAsync();
        }
        var deleted = await client.GetAsync($"/api/v1/groups/{groupId}/members");

        await AssertIdenticalNotFound(revoked, unrelated, missing, deleted);
    }

    [Test]
    public async Task Removing_a_member_revokes_the_retained_row_clears_the_envelope_and_blocks_reads_immediately()
    {
        var ownerId = await CurrentUserId(client);
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active, [131, 132]);
        var sharedRecordId = await SeedSharedRecord(groupId, ownerId);
        var readableBeforeRemoval = await memberClient.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes");
        readableBeforeRemoval!.Records.Should().Contain(record => record.Id == sharedRecordId);
        DateTime previousUpdatedAt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            previousUpdatedAt = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
                .GroupMemberships.Where(membership => membership.GroupId == groupId && membership.UserId == memberId)
                .Select(membership => membership.UpdatedAt)
                .SingleAsync();
        }
        await Task.Delay(10);

        var response = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{memberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<KeyRotationContract>())!.KeyRotationRequired.Should().BeTrue();
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}/members")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var readableAfterRemoval = await memberClient.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes");
        readableAfterRemoval!.Records.Should().NotContain(record => record.Id == sharedRecordId);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var membership = await verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership =>
                membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().Be(MembershipState.Revoked);
        membership.RevokedAt.Should().NotBeNull();
        membership.UpdatedAt.Should().BeAfter(previousUpdatedAt);
        membership.GroupKeyEnvelope.Should().BeNull();
    }

    [Test]
    public async Task Removing_an_active_member_without_an_envelope_still_requests_key_rotation()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active, null);

        var response = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{memberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<KeyRotationContract>())!.KeyRotationRequired.Should().BeTrue();
    }

    [Test]
    public async Task Only_the_owner_can_remove_a_different_active_member_from_the_same_group()
    {
        var ownerId = await CurrentUserId(client);
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var secondMemberId = await SeedUser();
        var awaitingId = await SeedUser();
        var revokedId = await SeedUser();
        var crossGroupId = await SeedUser();
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active);
        await AddMembership(groupId, secondMemberId, MembershipState.Active);
        await AddMembership(groupId, awaitingId, MembershipState.AwaitingOwnerApproval);
        await AddMembership(groupId, revokedId, MembershipState.Revoked);
        await SeedGroup(ownerId, crossGroupId, MembershipRole.Member, MembershipState.Active, false, [171]);

        var memberAttempt = await memberClient.DeleteAsync($"/api/v1/groups/{groupId}/members/{secondMemberId}");
        var selfAttempt = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{ownerId}");
        var awaitingAttempt = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{awaitingId}");
        var revokedAttempt = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{revokedId}");
        var crossGroupAttempt = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{crossGroupId}");
        var missingAttempt = await client.DeleteAsync($"/api/v1/groups/{groupId}/members/{Guid.CreateVersion7()}");

        await AssertIdenticalNotFound(
            memberAttempt,
            selfAttempt,
            awaitingAttempt,
            revokedAttempt,
            crossGroupAttempt,
            missingAttempt);
    }

    [Test]
    public async Task A_member_can_leave_and_loses_group_access_immediately()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active, [141, 142]);

        var response = await memberClient.PostAsync($"/api/v1/groups/{groupId}/leave", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<KeyRotationContract>())!.KeyRotationRequired.Should().BeTrue();
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var membership = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership =>
                membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().Be(MembershipState.Revoked);
        membership.GroupKeyEnvelope.Should().BeNull();
    }

    [Test]
    public async Task Leaving_an_active_member_without_an_envelope_still_requests_key_rotation()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active, null);
        DateTime previousUpdatedAt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            previousUpdatedAt = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
                .GroupMemberships.Where(membership =>
                    membership.GroupId == groupId && membership.UserId == memberId)
                .Select(membership => membership.UpdatedAt)
                .SingleAsync();
        }
        await Task.Delay(10);

        var response = await memberClient.PostAsync($"/api/v1/groups/{groupId}/leave", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<KeyRotationContract>())!.KeyRotationRequired.Should().BeTrue();
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var membership = await verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership =>
                membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().Be(MembershipState.Revoked);
        membership.GroupKeyEnvelope.Should().BeNull();
        membership.RevokedAt.Should().NotBeNull();
        membership.RevokedAt.Should().Be(membership.UpdatedAt);
        membership.RevokedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        membership.UpdatedAt.Should().BeAfter(previousUpdatedAt);
    }

    [Test]
    public async Task The_owner_cannot_leave_before_transferring_ownership()
    {
        var ownerId = await CurrentUserId(client);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;

        var response = await client.PostAsync($"/api/v1/groups/{groupId}/leave", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        var membership = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership =>
                membership.GroupId == groupId && membership.UserId == ownerId);
        membership.State.Should().Be(MembershipState.Active);
        membership.Role.Should().Be(MembershipRole.Owner);
        membership.GroupKeyEnvelope.Should().NotBeNull();
    }

    [Test]
    public async Task Transferring_ownership_updates_group_and_both_roles_atomically_without_changing_envelopes()
    {
        var ownerId = await CurrentUserId(client);
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, memberId, MembershipState.Active, [151, 152]);
        GroupMembership[] before;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            before = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
                .GroupMemberships.AsNoTracking().Where(membership => membership.GroupId == groupId)
                .OrderBy(membership => membership.UserId).ToArrayAsync();
        }
        await Task.Delay(10);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/ownership",
            new OwnershipRequest(memberId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
        var after = await dbContext.GroupMemberships.AsNoTracking()
            .Where(membership => membership.GroupId == groupId)
            .OrderBy(membership => membership.UserId).ToArrayAsync();
        group.OwnerUserId.Should().Be(memberId);
        after.Should().ContainSingle(membership =>
            membership.UserId == memberId &&
            membership.Role == MembershipRole.Owner &&
            membership.State == MembershipState.Active);
        after.Should().ContainSingle(membership =>
            membership.UserId == ownerId &&
            membership.Role == MembershipRole.Member &&
            membership.State == MembershipState.Active);
        after.Count(membership => membership.State == MembershipState.Active && membership.Role == MembershipRole.Owner)
            .Should().Be(1);
        after.Select(membership => membership.GroupKeyEnvelope).Should()
            .BeEquivalentTo(before.Select(membership => membership.GroupKeyEnvelope));
        after.Should().OnlyContain(membership =>
            membership.UpdatedAt > before.Single(prior => prior.Id == membership.Id).UpdatedAt);
        (await client.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}",
            new UpdateRequest(Convert.ToBase64String([153]), Convert.ToBase64String([154]), 1)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await memberClient.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}",
            new UpdateRequest(Convert.ToBase64String([155]), Convert.ToBase64String([156]), 1)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Ownership_transfer_rejects_self_and_every_ineligible_target_neutrally()
    {
        var ownerId = await CurrentUserId(client);
        var nonmemberId = await SeedUser();
        var awaitingId = await SeedUser();
        var revokedId = await SeedUser();
        var missingEnvelopeId = await SeedUser();
        var crossGroupId = await SeedUser();
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, awaitingId, MembershipState.AwaitingOwnerApproval);
        await AddMembership(groupId, revokedId, MembershipState.Revoked);
        await AddMembership(groupId, missingEnvelopeId, MembershipState.Active, null);
        await SeedGroup(ownerId, crossGroupId, MembershipRole.Member, MembershipState.Active, false, [181]);

        var self = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(ownerId));
        var nonmember = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(nonmemberId));
        var awaiting = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(awaitingId));
        var revoked = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(revokedId));
        var missingEnvelope = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(missingEnvelopeId));
        var crossGroup = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(crossGroupId));
        var missing = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(Guid.CreateVersion7()));
        var invalid = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(Guid.Empty));

        await AssertIdenticalNotFound(self, nonmember, awaiting, revoked, missingEnvelope, crossGroup, missing);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Ownership_transfer_rejects_a_group_with_multiple_active_owners_without_mutation()
    {
        var ownerId = await CurrentUserId(client);
        var targetId = await SeedUser();
        var thirdOwnerId = await SeedUser();
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, targetId, MembershipState.Active, [231]);
        await AddMembership(groupId, thirdOwnerId, MembershipRole.Owner, MembershipState.Active, [232]);
        var before = await LoadMembershipSnapshot(groupId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/ownership",
            new OwnershipRequest(targetId));

        await AssertIdenticalNotFound(response);
        (await GroupOwner(groupId)).Should().Be(ownerId);
        (await LoadMembershipSnapshot(groupId)).Should().BeEquivalentTo(before);
    }

    [Test]
    public async Task Ownership_transfer_rejects_stale_owner_metadata_without_mutation()
    {
        var metadataOwnerId = await CurrentUserId(client);
        var targetId = await SeedUser();
        var staleOwnerId = await SeedUser();
        var groupId = await SeedGroup(
            metadataOwnerId,
            metadataOwnerId,
            MembershipRole.Member,
            MembershipState.Active,
            false,
            [241]);
        await AddMembership(groupId, targetId, MembershipState.Active, [242]);
        await AddMembership(groupId, staleOwnerId, MembershipRole.Owner, MembershipState.Active, [243]);
        var before = await LoadMembershipSnapshot(groupId);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/ownership",
            new OwnershipRequest(targetId));

        await AssertIdenticalNotFound(response);
        (await GroupOwner(groupId)).Should().Be(metadataOwnerId);
        (await LoadMembershipSnapshot(groupId)).Should().BeEquivalentTo(before);
    }

    [Test]
    public async Task Membership_and_ownership_save_failures_leave_every_row_unchanged()
    {
        await using var failingFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<GroupMembership>());
        using var ownerClient = await failingFactory.CreateAuthenticatedClient();
        var ownerId = await CurrentUserId(ownerClient);
        using var memberClient = await failingFactory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var groupId = await SeedGroup(ownerId, ownerId, MembershipRole.Owner, MembershipState.Active, false, [191]);
        await AddMembership(groupId, memberId, MembershipState.Active, [192]);

        var remove = await ownerClient.DeleteAsync($"/api/v1/groups/{groupId}/members/{memberId}");
        var transfer = await ownerClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/ownership",
            new OwnershipRequest(memberId));

        remove.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        transfer.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
        var memberships = await dbContext.GroupMemberships.AsNoTracking()
            .Where(membership => membership.GroupId == groupId).ToArrayAsync();
        group.OwnerUserId.Should().Be(ownerId);
        memberships.Should().ContainSingle(membership =>
            membership.UserId == ownerId &&
            membership.Role == MembershipRole.Owner &&
            membership.State == MembershipState.Active);
        memberships.Should().ContainSingle(membership =>
            membership.UserId == memberId &&
            membership.Role == MembershipRole.Member &&
            membership.State == MembershipState.Active &&
            membership.GroupKeyEnvelope != null);
    }

    [Test]
    public async Task Concurrent_ownership_transfers_produce_exactly_one_new_owner()
    {
        var ownerId = await CurrentUserId(client);
        var firstTargetId = await SeedUser();
        var secondTargetId = await SeedUser();
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, firstTargetId, MembershipState.Active, [201]);
        await AddMembership(groupId, secondTargetId, MembershipState.Active, [202]);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(firstTargetId)),
            client.PostAsJsonAsync($"/api/v1/groups/{groupId}/ownership", new OwnershipRequest(secondTargetId)));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NoContent);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
        var memberships = await dbContext.GroupMemberships.AsNoTracking()
            .Where(membership => membership.GroupId == groupId).ToArrayAsync();
        memberships.Should().ContainSingle(membership =>
            membership.UserId == group.OwnerUserId &&
            membership.Role == MembershipRole.Owner &&
            membership.State == MembershipState.Active);
        memberships.Count(membership => membership.Role == MembershipRole.Owner && membership.State == MembershipState.Active)
            .Should().Be(1);
        group.OwnerUserId.Should().NotBe(ownerId);
    }

    [Test]
    public async Task Concurrent_transfer_and_target_leave_never_produce_a_revoked_or_duplicate_owner()
    {
        var ownerId = await CurrentUserId(client);
        using var targetClient = await factory.CreateAuthenticatedClient();
        var targetId = await CurrentUserId(targetClient);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/groups", ValidCreate());
        var groupId = (await createdResponse.Content.ReadFromJsonAsync<GroupContract>())!.Id;
        await AddMembership(groupId, targetId, MembershipState.Active, [211]);

        var transferTask = client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/ownership",
            new OwnershipRequest(targetId));
        var leaveTask = targetClient.PostAsync($"/api/v1/groups/{groupId}/leave", null);
        await Task.WhenAll(transferTask, leaveTask);

        new[] { transferTask.Result.StatusCode, leaveTask.Result.StatusCode }.Should().SatisfyRespectively(
            transferStatus => transferStatus.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound),
            leaveStatus => leaveStatus.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest));
        (transferTask.Result.StatusCode, leaveTask.Result.StatusCode).Should().BeOneOf(
            (HttpStatusCode.NoContent, HttpStatusCode.BadRequest),
            (HttpStatusCode.NotFound, HttpStatusCode.OK));
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.AsNoTracking().SingleAsync(group => group.Id == groupId);
        var memberships = await dbContext.GroupMemberships.AsNoTracking()
            .Where(membership => membership.GroupId == groupId).ToArrayAsync();
        memberships.Should().ContainSingle(membership =>
            membership.UserId == group.OwnerUserId &&
            membership.Role == MembershipRole.Owner &&
            membership.State == MembershipState.Active);
        memberships.Count(membership => membership.Role == MembershipRole.Owner && membership.State == MembershipState.Active)
            .Should().Be(1);
        memberships.Should().NotContain(membership =>
            membership.Role == MembershipRole.Owner && membership.State == MembershipState.Revoked);
        new[] { ownerId, targetId }.Should().Contain(group.OwnerUserId);
    }

    [Test]
    public async Task Every_membership_and_ownership_route_requires_authentication()
    {
        using var anonymous = factory.CreateClient();
        var groupId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var responses = new[]
        {
            await anonymous.GetAsync($"/api/v1/groups/{groupId}/members"),
            await anonymous.DeleteAsync($"/api/v1/groups/{groupId}/members/{userId}"),
            await anonymous.PostAsync($"/api/v1/groups/{groupId}/leave", null),
            await anonymous.PostAsJsonAsync(
                $"/api/v1/groups/{groupId}/ownership",
                new OwnershipRequest(userId))
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    private static CreateRequest ValidCreate() => new(
        Convert.ToBase64String([1, 2, 3]),
        Convert.ToBase64String([4, 5, 6]),
        1,
        Convert.ToBase64String([7, 8, 9]));

    private static async Task<Guid> CurrentUserId(HttpClient actor)
    {
        var identity = await actor.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        return identity!.Id;
    }

    private async Task<Guid> SeedGroup(
        Guid ownerUserId,
        Guid membershipUserId,
        MembershipRole role,
        MembershipState state,
        bool isDeleted,
        byte[]? envelope)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var groupId = Guid.CreateVersion7();
        dbContext.Groups.Add(new Group
        {
            Id = groupId,
            OwnerUserId = ownerUserId,
            NameCiphertext = [21, 22],
            NameNonce = [23, 24],
            ProtocolVersion = 1,
            IsDeleted = isDeleted,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = membershipUserId,
            Role = role,
            State = state,
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RevokedAt = state == MembershipState.Revoked ? now : null
        });
        await dbContext.SaveChangesAsync();
        return groupId;
    }

    private Task AddMembership(Guid groupId, Guid userId, MembershipState state) =>
        AddMembership(groupId, userId, MembershipRole.Member, state, [71]);

    private Task AddMembership(
        Guid groupId,
        Guid userId,
        MembershipState state,
        byte[]? envelope) =>
        AddMembership(groupId, userId, MembershipRole.Member, state, envelope);

    private async Task AddMembership(
        Guid groupId,
        Guid userId,
        MembershipRole role,
        MembershipState state,
        byte[]? envelope)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = userId,
            Role = role,
            State = state,
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RevokedAt = state == MembershipState.Revoked ? now : null
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> GroupOwner(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .Groups.Where(group => group.Id == groupId)
            .Select(group => group.OwnerUserId)
            .SingleAsync();
    }

    private async Task<MembershipSnapshot[]> LoadMembershipSnapshot(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.AsNoTracking()
            .Where(membership => membership.GroupId == groupId)
            .OrderBy(membership => membership.UserId)
            .Select(membership => new MembershipSnapshot(
                membership.Id,
                membership.UserId,
                membership.Role,
                membership.State,
                membership.GroupKeyEnvelope,
                membership.UpdatedAt,
                membership.RevokedAt))
            .ToArrayAsync();
    }

    private async Task<Guid> SeedSharedRecord(Guid groupId, Guid ownerUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var resourceId = Guid.CreateVersion7();
        var recordId = Guid.CreateVersion7();
        dbContext.SharedResources.Add(new SharedResource
        {
            Id = resourceId,
            Type = SharedResourceType.Account,
            OwnerUserId = ownerUserId,
            CreatedAt = now
        });
        dbContext.ResourceGrants.Add(new ResourceGrant
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            ResourceType = SharedResourceType.Account,
            ResourceId = resourceId,
            Permission = GrantPermission.Viewer,
            State = GrantState.Active,
            GrantedByUserId = ownerUserId,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.EncryptedRecords.Add(new EncryptedRecord
        {
            Id = recordId,
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerUserId,
            ParentResourceId = resourceId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [161],
            Ciphertext = [162],
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        return recordId;
    }

    private async Task<Guid> SeedUser()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = Guid.CreateVersion7();
        var email = $"group-seed-{userId:N}@example.test";
        dbContext.Users.Add(new XpenseUser
        {
            Id = userId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            State = AccountState.Active,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static async Task AssertIdenticalNotFound(params HttpResponseMessage[] responses)
    {
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsByteArrayAsync()));
        bodies.Should().OnlyContain(body => body.SequenceEqual(bodies[0]));
    }

    private sealed record CreateRequest(
        string? NameCiphertext,
        string? NameNonce,
        int ProtocolVersion,
        string? OwnerKeyEnvelope);

    private sealed record IdentityContract(Guid Id, string Email);

    private sealed record GroupMemberContract(
        Guid UserId,
        string Email,
        MembershipRole Role,
        bool HasKeyEnvelope);

    private sealed record KeyRotationContract(bool KeyRotationRequired);

    private sealed record OwnershipRequest(Guid NewOwnerUserId);

    private sealed record MembershipSnapshot(
        Guid Id,
        Guid UserId,
        MembershipRole Role,
        MembershipState State,
        byte[]? GroupKeyEnvelope,
        DateTime UpdatedAt,
        DateTime? RevokedAt);

    private sealed record SyncChangesContract(SyncRecordContract[] Records);

    private sealed record SyncRecordContract(Guid Id);

    private sealed record UpdateRequest(
        string? NameCiphertext,
        string? NameNonce,
        int ProtocolVersion);

    private sealed record GroupContract(
        Guid Id,
        Guid OwnerUserId,
        MembershipRole Role,
        string NameCiphertext,
        string NameNonce,
        int ProtocolVersion,
        string? GroupKeyEnvelope,
        int EnvelopeProtocolVersion,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed class GroupLockAttemptInterceptor : DbCommandInterceptor
    {
        private TaskCompletionSource? nextAttempt;

        public Task WaitForNextAttempt()
        {
            nextAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return nextAttempt.Task;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
                nextAttempt?.TrySetResult();

            return ValueTask.FromResult(result);
        }
    }

    private sealed class CancelLockCommitAfterMainCommitInterceptor : DbTransactionInterceptor
    {
        private int groupMutationCommitted;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<Group>().Any() == true)
                Interlocked.Exchange(ref groupMutationCommitted, 1);

            return Task.CompletedTask;
        }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref groupMutationCommitted) == 1 &&
                eventData.Context?.ChangeTracker.Entries<Group>().Any() != true)
                throw new OperationCanceledException("Simulated request cancellation after the main commit");

            return ValueTask.FromResult(result);
        }
    }

    public enum RosterRaceMode
    {
        RevokeCaller,
        DeleteGroup
    }

    private sealed class RosterRaceInterceptor(
        string connectionString,
        RosterRaceMode mode) : DbCommandInterceptor
    {
        private Guid groupId;
        private Guid userId;
        private int armed;

        public void Arm(Guid value, Guid callerId)
        {
            groupId = value;
            userId = callerId;
            Interlocked.Exchange(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1 &&
                command.CommandText.Contains("JOIN \"Xpense\".\"Users\"", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref armed, 0) == 1)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                var commandText = mode == RosterRaceMode.RevokeCaller
                    ? "UPDATE \"Xpense\".\"GroupMemberships\" SET \"State\" = 2, \"GroupKeyEnvelope\" = NULL, \"RevokedAt\" = NOW(), \"UpdatedAt\" = NOW() WHERE \"GroupId\" = @groupId AND \"UserId\" = @userId"
                    : "UPDATE \"Xpense\".\"Groups\" SET \"IsDeleted\" = TRUE, \"UpdatedAt\" = NOW() WHERE \"Id\" = @groupId";
                await using var mutation = new NpgsqlCommand(commandText, connection);
                mutation.Parameters.AddWithValue("groupId", groupId);
                if (mode == RosterRaceMode.RevokeCaller)
                    mutation.Parameters.AddWithValue("userId", userId);
                await mutation.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
