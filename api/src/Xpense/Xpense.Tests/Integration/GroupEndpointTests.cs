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
        AddMembership(groupId, userId, state, [71]);

    private async Task AddMembership(
        Guid groupId,
        Guid userId,
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
            Role = MembershipRole.Member,
            State = state,
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RevokedAt = state == MembershipState.Revoked ? now : null
        });
        await dbContext.SaveChangesAsync();
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

    private sealed record IdentityContract(Guid Id);

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
}
