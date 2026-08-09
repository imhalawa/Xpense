using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class InvitationEndpointTests
{
    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;
    private string connectionString = null!;
    private Guid ownerId;
    private Guid groupId;

    [SetUp]
    public async Task SetUp()
    {
        connectionString = await PostgresFixture.CreateDatabase();
        factory = new WebApiTestFactory(connectionString);
        client = await factory.CreateAuthenticatedClient();
        ownerId = await CurrentUserId(client);
        groupId = await SeedGroup(factory.Services, ownerId);
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task Creating_an_invitation_returns_a_one_time_link_and_stores_only_the_raw_token_hash()
    {
        var response = await client.PostAsJsonAsync("/api/v1/invitations", ValidCreate());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeTrue();
        var created = await response.Content.ReadFromJsonAsync<CreatedInvitationContract>();
        created.Should().NotBeNull();
        created!.InvitationLink.Should().StartWith("https://app.example.test/invitations/");
        response.Headers.Location.AbsolutePath.Should().Be($"/api/v1/invitations/{created.Id}");
        created.GroupId.Should().Be(groupId);
        created.TargetEmail.Should().BeNull();
        created.State.Should().Be(InvitationState.Pending);
        created.HasKeyEnvelope.Should().BeFalse();
        var createdJson = await response.Content.ReadAsStringAsync();
        createdJson.Should().NotContain("tokenHash");
        createdJson.Should().NotContain("groupKeyEnvelope");
        createdJson.Should().NotContain("protocolVersion");
        var token = created.InvitationLink.Split('/').Last();
        var rawToken = WebEncoders.Base64UrlDecode(token);
        rawToken.Should().HaveCount(32);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync();
        invitation.Id.Should().Be(created.Id);
        invitation.TokenHash.Should().Equal(SHA256.HashData(rawToken));
        invitation.TokenHash.Should().NotEqual(rawToken);
        invitation.ExpiresAt.Should().Be(invitation.CreatedAt.AddDays(7));
        created.ExpiresAt.Should().Be(invitation.ExpiresAt);
        invitation.TargetNormalizedEmail.Should().BeNull();
        invitation.GroupKeyEnvelope.Should().BeNull();
        invitation.EnvelopeProtocolVersion.Should().BeNull();
        (await dbContext.InvitationDeliveries.CountAsync()).Should().Be(0);
        var eventRecord = await dbContext.Events.SingleAsync();
        eventRecord.Type.Should().Be(nameof(GroupInvitationCreated));
        var eventBody = JsonNode.Parse(eventRecord.Body)!.AsObject();
        eventBody.Select(property => property.Key).Should().Equal("invitationId");
        eventBody["invitationId"]!.GetValue<Guid>().Should().Be(invitation.Id);
        eventRecord.Body.Should().NotContain(token);
        eventRecord.Body.Should().NotContain(created.InvitationLink);

        var listJson = await (await client.GetAsync($"/api/v1/invitations?groupId={groupId}"))
            .Content.ReadAsStringAsync();
        listJson.Should().NotContain("invitationLink");
        listJson.Should().NotContain("tokenHash");
        listJson.Should().NotContain("groupKeyEnvelope");
        listJson.Should().NotContain(token);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var databaseTextCommand = new NpgsqlCommand(
            """
            SELECT
                COALESCE((SELECT string_agg(to_jsonb(invitation)::text, '') FROM "Xpense"."GroupInvitations" invitation), '') ||
                COALESCE((SELECT string_agg(to_jsonb(delivery)::text, '') FROM "Xpense"."InvitationDeliveries" delivery), '') ||
                COALESCE((SELECT string_agg(to_jsonb(event_record)::text, '') FROM "Xpense"."Events" event_record), '')
            """,
            connection);
        var databaseText = (string)(await databaseTextCommand.ExecuteScalarAsync())!;
        databaseText.Should().NotContain(token);
        databaseText.Should().NotContain(created.InvitationLink);
    }

    [Test]
    public async Task A_targeted_invitation_stores_the_known_users_envelope_and_a_protected_delivery_link()
    {
        var targetEmail = "Known.User@Example.Test";
        await SeedUser(targetEmail);
        var envelope = Convert.ToBase64String([41, 42, 43]);

        var response = await client.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(groupId, $"  {targetEmail}  ", envelope, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CreatedInvitationContract>())!;
        created.TargetEmail.Should().Be(targetEmail);
        created.HasKeyEnvelope.Should().BeTrue();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync();
        invitation.TargetNormalizedEmail.Should().Be(targetEmail.ToUpperInvariant());
        invitation.GroupKeyEnvelope.Should().Equal([41, 42, 43]);
        invitation.EnvelopeProtocolVersion.Should().Be(1);
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.InvitationId.Should().Be(invitation.Id);
        delivery.EmailAddress.Should().Be(targetEmail);
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.ProtectedPayload.Should().NotBeNullOrEmpty();
        delivery.ProtectedPayload.Should().NotContain(created.InvitationLink);
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Xpense.InvitationDelivery");
        protector.Unprotect(delivery.ProtectedPayload!).Should().Be(created.InvitationLink);
        (await dbContext.Events.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task A_target_email_without_an_envelope_is_deliverable_without_requiring_an_existing_user()
    {
        const string targetEmail = "new.person@example.test";

        var response = await client.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(groupId, $" {targetEmail} ", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync();
        invitation.TargetNormalizedEmail.Should().Be("NEW.PERSON@EXAMPLE.TEST");
        invitation.GroupKeyEnvelope.Should().BeNull();
        invitation.EnvelopeProtocolVersion.Should().BeNull();
        (await dbContext.InvitationDeliveries.SingleAsync()).EmailAddress.Should().Be(targetEmail);
    }

    [Test]
    public async Task Listing_invitations_returns_the_owners_active_invitations()
    {
        await SeedInvitation(InvitationState.Pending, " pending@example.test ", [51]);
        await SeedInvitation(InvitationState.AwaitingOwnerApproval, "awaiting@example.test", null);
        await SeedInvitation(InvitationState.Accepted);
        await SeedInvitation(InvitationState.Revoked);

        var response = await client.GetAsync($"/api/v1/invitations?groupId={groupId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invitations = await response.Content.ReadFromJsonAsync<InvitationContract[]>();
        invitations.Should().HaveCount(2);
        invitations.Should().OnlyContain(invitation =>
            invitation.State == InvitationState.Pending ||
            invitation.State == InvitationState.AwaitingOwnerApproval);
        invitations.Should().ContainSingle(invitation =>
            invitation.TargetEmail == "pending@example.test" && invitation.HasKeyEnvelope);
        invitations.Should().ContainSingle(invitation =>
            invitation.TargetEmail == "awaiting@example.test" && !invitation.HasKeyEnvelope);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("invitationLink");
        json.Should().NotContain("tokenHash");
        json.Should().NotContain("groupKeyEnvelope");
    }

    [Test]
    public async Task Invitation_creation_rejects_the_full_validation_matrix_without_writes()
    {
        var targetEmail = "known@example.test";
        await SeedUser(targetEmail);
        var validEnvelope = Convert.ToBase64String([61]);
        var valid = new CreateInvitationRequest(groupId, targetEmail, validEnvelope, 1);
        var invalid = new[]
        {
            valid with { GroupId = Guid.Empty },
            valid with { TargetEmail = " " },
            valid with { TargetEmail = "not-an-email" },
            valid with { TargetEmail = $"{new string('a', 250)}@example.test" },
            valid with { GroupKeyEnvelope = null, TargetEmail = null, ProtocolVersion = 0 },
            valid with { ProtocolVersion = 2 },
            valid with { GroupKeyEnvelope = string.Empty },
            valid with { GroupKeyEnvelope = "not-base64" },
            valid with { GroupKeyEnvelope = "AR==" },
            valid with { GroupKeyEnvelope = Convert.ToBase64String(new byte[4097]) },
            valid with { GroupKeyEnvelope = validEnvelope, TargetEmail = null }
        };

        foreach (var request in invalid)
        {
            var response = await client.PostAsJsonAsync("/api/v1/invitations", request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await AssertNoInvitationWrites(factory.Services);
    }

    [Test]
    public async Task An_envelope_for_an_unknown_target_is_rejected_after_owner_authorization_without_writes()
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(
                groupId,
                "unknown@example.test",
                Convert.ToBase64String([71]),
                1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("registered target email");
        await AssertNoInvitationWrites(factory.Services);
    }

    [Test]
    public async Task Nonowners_and_missing_or_deleted_groups_receive_identical_create_and_list_not_found()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        using var unrelatedClient = await factory.CreateAuthenticatedClient();
        await AddMembership(groupId, memberId);
        var request = new CreateInvitationRequest(
            groupId,
            "unknown@example.test",
            Convert.ToBase64String([81]),
            1);

        var memberCreate = await memberClient.PostAsJsonAsync("/api/v1/invitations", request);
        var unrelatedCreate = await unrelatedClient.PostAsJsonAsync("/api/v1/invitations", request);
        var missingCreate = await client.PostAsJsonAsync(
            "/api/v1/invitations",
            request with { GroupId = Guid.CreateVersion7() });
        var memberList = await memberClient.GetAsync($"/api/v1/invitations?groupId={groupId}");
        var unrelatedList = await unrelatedClient.GetAsync($"/api/v1/invitations?groupId={groupId}");
        var missingList = await client.GetAsync($"/api/v1/invitations?groupId={Guid.CreateVersion7()}");
        await DeleteGroupDirectly(groupId);
        var deletedCreate = await client.PostAsJsonAsync("/api/v1/invitations", request);
        var deletedList = await client.GetAsync($"/api/v1/invitations?groupId={groupId}");

        await AssertIdenticalNotFound(memberCreate, unrelatedCreate, missingCreate, deletedCreate);
        await AssertIdenticalNotFound(memberList, unrelatedList, missingList, deletedList);
        await AssertNoInvitationWrites(factory.Services);
    }

    [Test]
    public async Task Revoking_a_pending_invitation_returns_no_content()
    {
        var invitationId = await SeedInvitation(InvitationState.Pending);

        var response = await client.DeleteAsync($"/api/v1/invitations/{invitationId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var scope = factory.Services.CreateAsyncScope();
        var invitation = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupInvitations.SingleAsync(invitation => invitation.Id == invitationId);
        invitation.State.Should().Be(InvitationState.Revoked);
        invitation.UpdatedAt.Should().BeAfter(invitation.CreatedAt);
        var listed = await client.GetFromJsonAsync<InvitationContract[]>(
            $"/api/v1/invitations?groupId={groupId}");
        listed.Should().NotContain(item => item.Id == invitationId);
    }

    [Test]
    public async Task Revoking_awaiting_succeeds_while_other_states_and_callers_are_neutral()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        using var unrelatedClient = await factory.CreateAuthenticatedClient();
        await AddMembership(groupId, memberId);
        var awaitingId = await SeedInvitation(InvitationState.AwaitingOwnerApproval);
        var acceptedId = await SeedInvitation(InvitationState.Accepted);
        var revokedId = await SeedInvitation(InvitationState.Revoked);
        var deletedId = await SeedInvitation(InvitationState.Pending);

        var awaiting = await client.DeleteAsync($"/api/v1/invitations/{awaitingId}");
        var accepted = await client.DeleteAsync($"/api/v1/invitations/{acceptedId}");
        var revoked = await client.DeleteAsync($"/api/v1/invitations/{revokedId}");
        var member = await memberClient.DeleteAsync($"/api/v1/invitations/{acceptedId}");
        var unrelated = await unrelatedClient.DeleteAsync($"/api/v1/invitations/{acceptedId}");
        var missing = await client.DeleteAsync($"/api/v1/invitations/{Guid.CreateVersion7()}");
        await DeleteGroupDirectly(groupId);
        var deleted = await client.DeleteAsync($"/api/v1/invitations/{deletedId}");

        awaiting.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertIdenticalNotFound(accepted, revoked, member, unrelated, missing, deleted);
    }

    [Test]
    public async Task Concurrent_revocation_has_exactly_one_winner()
    {
        var invitationId = await SeedInvitation(InvitationState.Pending);

        var responses = await Task.WhenAll(
            client.DeleteAsync($"/api/v1/invitations/{invitationId}"),
            client.DeleteAsync($"/api/v1/invitations/{invitationId}"));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NoContent);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_persistence_failure_leaves_no_invitation_delivery_or_event()
    {
        await using var failingFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<GroupInvitation>());
        using var failingClient = await failingFactory.CreateAuthenticatedClient();
        var failingOwnerId = await CurrentUserId(failingClient);
        var failingGroupId = await SeedGroup(failingFactory.Services, failingOwnerId);

        var response = await failingClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(failingGroupId, "target@example.test", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await AssertNoInvitationWrites(failingFactory.Services);
    }

    [Test]
    public async Task A_data_protection_failure_leaves_no_invitation_delivery_or_event()
    {
        await using var failingFactory = new WebApiTestFactory(connectionString)
            .WithDataProtectionProvider(new ThrowingDataProtectionProvider());
        using var failingClient = await failingFactory.CreateAuthenticatedClient();
        var failingOwnerId = await CurrentUserId(failingClient);
        var failingGroupId = await SeedGroup(failingFactory.Services, failingOwnerId);

        var response = await failingClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(failingGroupId, "target@example.test", null, 1));

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await AssertNoInvitationWrites(failingFactory.Services);
    }

    [Test]
    public async Task A_waiting_invitation_create_observes_a_group_delete_committed_under_the_same_lock()
    {
        var lockInterceptor = new GroupLockAttemptInterceptor();
        await using var lockFactory = new WebApiTestFactory(connectionString, lockInterceptor);
        using var lockClient = await lockFactory.CreateAuthenticatedClient();
        var lockOwnerId = await CurrentUserId(lockClient);
        var lockGroupId = await SeedGroup(lockFactory.Services, lockOwnerId);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("key", lockGroupId.ToString("N"));
            await lockCommand.ExecuteNonQueryAsync();
        }
        await using (var deleteCommand = new NpgsqlCommand(
            "UPDATE \"Xpense\".\"Groups\" SET \"IsDeleted\" = TRUE, \"UpdatedAt\" = NOW() WHERE \"Id\" = @id",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("id", lockGroupId);
            await deleteCommand.ExecuteNonQueryAsync();
        }
        var lockAttempt = lockInterceptor.WaitForNextAttempt();
        var createTask = lockClient.PostAsJsonAsync(
            "/api/v1/invitations",
            new CreateInvitationRequest(lockGroupId, null, null, 1));
        await lockAttempt.WaitAsync(TimeSpan.FromSeconds(5));

        await transaction.CommitAsync();
        var response = await createTask;

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNoInvitationWrites(lockFactory.Services);
    }

    [Test]
    public async Task A_list_query_rechecks_owner_access_after_authorization_before_returning_target_emails()
    {
        var raceInterceptor = new InvitationListRaceInterceptor(connectionString);
        await using var raceFactory = new WebApiTestFactory(connectionString, raceInterceptor);
        using var raceClient = await raceFactory.CreateAuthenticatedClient();
        var raceOwnerId = await CurrentUserId(raceClient);
        var raceGroupId = await SeedGroup(raceFactory.Services, raceOwnerId);
        await SeedInvitation(
            raceFactory.Services,
            raceGroupId,
            raceOwnerId,
            InvitationState.Pending,
            "private@example.test",
            null);
        raceInterceptor.Arm(raceGroupId);

        var response = await raceClient.GetAsync($"/api/v1/invitations?groupId={raceGroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<InvitationContract[]>()).Should().BeEmpty();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("private@example.test");
    }

    [Test]
    public async Task Every_invitation_management_route_requires_authentication()
    {
        using var anonymous = factory.CreateClient();

        var responses = new[]
        {
            await anonymous.PostAsJsonAsync("/api/v1/invitations", ValidCreate()),
            await anonymous.GetAsync($"/api/v1/invitations?groupId={groupId}"),
            await anonymous.DeleteAsync($"/api/v1/invitations/{Guid.CreateVersion7()}")
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task The_invitation_envelope_protocol_column_is_nullable_and_additive()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'Xpense' AND table_name = 'GroupInvitations' AND column_name = 'EnvelopeProtocolVersion'",
            connection);

        (await command.ExecuteScalarAsync()).Should().Be("YES");
    }

    [Test]
    public async Task The_migration_backfills_a_legacy_invitation_envelope_before_adding_the_constraint()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            await dbContext.GetService<IMigrator>()
                .MigrateAsync("20260809002953_AddPendingPasskeyAssertions");
        }

        var invitationId = Guid.CreateVersion7();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "Xpense"."GroupInvitations"
                    ("Id", "GroupId", "InvitedByUserId", "TokenHash", "State", "ExpiresAt", "GroupKeyEnvelope", "CreatedAt", "UpdatedAt")
                VALUES
                    (@id, @groupId, @ownerId, @tokenHash, 0, NOW() + INTERVAL '7 days', @envelope, NOW(), NOW())
                """,
                connection);
            command.Parameters.AddWithValue("id", invitationId);
            command.Parameters.AddWithValue("groupId", groupId);
            command.Parameters.AddWithValue("ownerId", ownerId);
            command.Parameters.AddWithValue("tokenHash", Guid.NewGuid().ToByteArray());
            command.Parameters.AddWithValue("envelope", new byte[] { 91, 92 });
            await command.ExecuteNonQueryAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await using var verifyConnection = new NpgsqlConnection(connectionString);
        await verifyConnection.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(
            "SELECT \"EnvelopeProtocolVersion\" FROM \"Xpense\".\"GroupInvitations\" WHERE \"Id\" = @id",
            verifyConnection);
        verifyCommand.Parameters.AddWithValue("id", invitationId);
        (await verifyCommand.ExecuteScalarAsync()).Should().Be(1);
    }

    private CreateInvitationRequest ValidCreate() => new(groupId, null, null, 1);

    private async Task<Guid> SeedInvitation(
        InvitationState state,
        string? targetEmail = null,
        byte[]? envelope = null) =>
        await SeedInvitation(factory.Services, groupId, ownerId, state, targetEmail, envelope);

    private static async Task<Guid> SeedInvitation(
        IServiceProvider services,
        Guid value,
        Guid inviterId,
        InvitationState state,
        string? targetEmail,
        byte[]? envelope)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var invitation = new GroupInvitation
        {
            Id = Guid.CreateVersion7(),
            GroupId = value,
            InvitedByUserId = inviterId,
            TargetNormalizedEmail = targetEmail?.Trim().ToUpperInvariant(),
            TokenHash = Guid.NewGuid().ToByteArray(),
            State = state,
            ExpiresAt = now.AddDays(7),
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = envelope is null ? null : 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.GroupInvitations.Add(invitation);
        if (targetEmail is not null)
        {
            dbContext.InvitationDeliveries.Add(new InvitationDelivery
            {
                Id = Guid.CreateVersion7(),
                InvitationId = invitation.Id,
                EmailAddress = targetEmail.Trim(),
                ProtectedPayload = "protected",
                Status = DeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await dbContext.SaveChangesAsync();
        return invitation.Id;
    }

    private async Task SeedUser(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Users.Add(new XpenseUser
        {
            Id = Guid.CreateVersion7(),
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
    }

    private async Task AddMembership(Guid value, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = value,
            UserId = userId,
            Role = MembershipRole.Member,
            State = MembershipState.Active,
            GroupKeyEnvelope = [101],
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task DeleteGroupDirectly(Guid value)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var group = await dbContext.Groups.SingleAsync(group => group.Id == value);
        group.MarkAsDeleted();
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertNoInvitationWrites(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.GroupInvitations.CountAsync()).Should().Be(0);
        (await dbContext.InvitationDeliveries.CountAsync()).Should().Be(0);
        (await dbContext.Events.CountAsync()).Should().Be(0);
    }

    private static async Task AssertIdenticalNotFound(params HttpResponseMessage[] responses)
    {
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsByteArrayAsync()));
        bodies.Should().OnlyContain(body => body.SequenceEqual(bodies[0]));
    }

    private static async Task<Guid> SeedGroup(IServiceProvider services, Guid ownerUserId)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            NameCiphertext = [1],
            NameNonce = [2],
            ProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Groups.Add(group);
        dbContext.GroupMemberships.Add(new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = group.Id,
            UserId = ownerUserId,
            Role = MembershipRole.Owner,
            State = MembershipState.Active,
            GroupKeyEnvelope = [3],
            EnvelopeProtocolVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        return group.Id;
    }

    private static async Task<Guid> CurrentUserId(HttpClient actor)
    {
        var identity = await actor.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me");
        return identity!.Id;
    }

    private sealed record CreateInvitationRequest(
        Guid GroupId,
        string? TargetEmail,
        string? GroupKeyEnvelope,
        int ProtocolVersion);

    private sealed record CreatedInvitationContract(
        Guid Id,
        Guid GroupId,
        string? TargetEmail,
        InvitationState State,
        DateTime ExpiresAt,
        bool HasKeyEnvelope,
        string InvitationLink);

    private sealed record InvitationContract(
        Guid Id,
        Guid GroupId,
        string? TargetEmail,
        InvitationState State,
        DateTime ExpiresAt,
        bool HasKeyEnvelope,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record IdentityContract(Guid Id);

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

    private sealed class InvitationListRaceInterceptor(string connectionString) : DbCommandInterceptor
    {
        private Guid groupId;
        private int armed;

        public void Arm(Guid value)
        {
            groupId = value;
            Interlocked.Exchange(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1 &&
                command.CommandText.Contains("InvitationDeliveries", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref armed, 0) == 1)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var mutation = new NpgsqlCommand(
                    "UPDATE \"Xpense\".\"Groups\" SET \"IsDeleted\" = TRUE, \"UpdatedAt\" = NOW() WHERE \"Id\" = @groupId",
                    connection);
                mutation.Parameters.AddWithValue("groupId", groupId);
                await mutation.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class ThrowingDataProtectionProvider : IDataProtectionProvider
    {
        private readonly IDataProtectionProvider fallback = new EphemeralDataProtectionProvider();

        public IDataProtector CreateProtector(string purpose) =>
            purpose == "Xpense.InvitationDelivery"
                ? new ThrowingDataProtector()
                : fallback.CreateProtector(purpose);
    }

    private sealed class ThrowingDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) =>
            throw new InvalidOperationException("Simulated data-protection failure");

        public byte[] Unprotect(byte[] protectedData) =>
            throw new InvalidOperationException("Simulated data-protection failure");
    }
}
