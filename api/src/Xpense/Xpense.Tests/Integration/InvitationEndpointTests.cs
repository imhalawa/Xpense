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
    private GroupLockAttemptInterceptor lockInterceptor = null!;
    private Guid ownerId;
    private Guid groupId;

    [SetUp]
    public async Task SetUp()
    {
        connectionString = await PostgresFixture.CreateDatabase();
        lockInterceptor = new GroupLockAttemptInterceptor();
        factory = new WebApiTestFactory(connectionString, lockInterceptor);
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
    public async Task Inspecting_a_pending_open_invitation_hides_group_ciphertext()
    {
        var created = await CreateInvitation(ValidCreate());
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/invitations/{Token(created)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var inspected = await response.Content.ReadFromJsonAsync<InspectInvitationContract>();
        inspected!.State.Should().Be(InvitationState.Pending);
        inspected.RequiresApproval.Should().BeTrue();
        inspected.InviterEmail.Should().NotBeNullOrWhiteSpace();
        inspected.NameCiphertext.Should().BeNull();
        inspected.NameNonce.Should().BeNull();
        inspected.ProtocolVersion.Should().BeNull();
    }

    [Test]
    public async Task Accepting_a_bound_invitation_with_an_envelope_activates_the_member()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        var envelope = Convert.ToBase64String([111, 112]);
        var created = await CreateInvitation(new CreateInvitationRequest(groupId, member.Email, envelope, 1));

        var response = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await response.Content.ReadFromJsonAsync<AcceptInvitationContract>();
        accepted!.GroupId.Should().Be(groupId);
        accepted.State.Should().Be(MembershipState.Active);
        accepted.RequiresApproval.Should().BeFalse();
        accepted.GroupKeyEnvelope.Should().Be(envelope);
        accepted.EnvelopeProtocolVersion.Should().Be(1);
    }

    [Test]
    public async Task The_owner_can_approve_an_awaiting_open_invitation()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var created = await CreateInvitation(ValidCreate());
        var accepted = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        await UpdateInvitation(created.Id, invitation => invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
        var approvedEnvelope = Convert.ToBase64String([121, 122]);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/invitations/{created.Id}/approve",
            new ApproveInvitationRequest(approvedEnvelope, 1));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var memberId = await CurrentUserId(memberClient);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().Be(MembershipState.Active);
        membership.GroupKeyEnvelope.Should().Equal([121, 122]);
        membership.EnvelopeProtocolVersion.Should().Be(1);
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id);
        invitation.State.Should().Be(InvitationState.Accepted);
        invitation.AcceptedByUserId.Should().Be(memberId);
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task The_owner_can_read_the_accepted_users_public_key_and_complete_open_invitation_approval()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        await SeedEncryptionIdentity(member.Id, [201, 202], [203, 204], [205], 1);
        var created = await CreateInvitation(ValidCreate());
        (await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)))).StatusCode.Should().Be(HttpStatusCode.OK);

        var contextResponse = await client.GetAsync(
            $"/api/v1/invitations/{created.Id}/approval-context");

        contextResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var context = await contextResponse.Content.ReadFromJsonAsync<ApprovalContextContract>();
        context.Should().Be(new ApprovalContextContract(
            created.Id,
            member.Id,
            Convert.ToBase64String([201, 202]),
            1));
        var contextJson = await contextResponse.Content.ReadAsStringAsync();
        contextJson.Should().NotContain("encryptedPrivateKey");
        contextJson.Should().NotContain("nonce");
        contextJson.Should().NotContain(Convert.ToBase64String([203, 204]));
        contextJson.Should().NotContain(Convert.ToBase64String([205]));

        var approvedEnvelope = Convert.ToBase64String([206, 207]);
        var approved = await client.PostAsJsonAsync(
            $"/api/v1/invitations/{created.Id}/approve",
            new ApproveInvitationRequest(approvedEnvelope, 1));

        approved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var unavailable = await client.GetAsync(
            $"/api/v1/invitations/{created.Id}/approval-context");
        unavailable.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Approval_context_is_neutral_for_nonowners_missing_and_nonawaiting_invitations()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        await SeedEncryptionIdentity(member.Id, [211], [212], [213], 1);
        var awaiting = await CreateInvitation(ValidCreate());
        (await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(awaiting)))).StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = await CreateInvitation(ValidCreate());

        var nonowner = await memberClient.GetAsync(
            $"/api/v1/invitations/{awaiting.Id}/approval-context");
        var wrongState = await client.GetAsync(
            $"/api/v1/invitations/{pending.Id}/approval-context");
        var missing = await client.GetAsync(
            $"/api/v1/invitations/{Guid.CreateVersion7()}/approval-context");

        await AssertIdenticalNotFound(nonowner, wrongState, missing);
    }

    [Test]
    public async Task Malformed_and_unknown_inspection_tokens_return_the_same_fixed_problem()
    {
        using var anonymous = factory.CreateClient();
        var malformed = await anonymous.GetAsync("/api/v1/invitations/malformed%%%token");
        var unknown = await anonymous.GetAsync(
            $"/api/v1/invitations/{Xpense.API.Infrastructure.Invitations.InvitationTokenCodec.Generate().Token}");

        malformed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var malformedBody = await malformed.Content.ReadAsByteArrayAsync();
        var unknownBody = await unknown.Content.ReadAsByteArrayAsync();
        malformedBody.Should().Equal(unknownBody);
        Encoding.UTF8.GetString(malformedBody).Should().Contain("This invitation is no longer valid.");
    }

    [Test]
    public async Task Every_nonpending_inspection_state_and_expiry_returns_the_same_fixed_problem()
    {
        var expired = await CreateInvitation(ValidCreate());
        var revoked = await CreateInvitation(ValidCreate());
        var awaiting = await CreateInvitation(ValidCreate());
        var accepted = await CreateInvitation(ValidCreate());
        await UpdateInvitation(expired.Id, invitation => invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
        await UpdateInvitation(revoked.Id, invitation => invitation.State = InvitationState.Revoked);
        await UpdateInvitation(awaiting.Id, invitation => invitation.State = InvitationState.AwaitingOwnerApproval);
        await UpdateInvitation(accepted.Id, invitation => invitation.State = InvitationState.Accepted);
        using var anonymous = factory.CreateClient();

        var responses = await Task.WhenAll(
            anonymous.GetAsync($"/api/v1/invitations/{Token(expired)}"),
            anonymous.GetAsync($"/api/v1/invitations/{Token(revoked)}"),
            anonymous.GetAsync($"/api/v1/invitations/{Token(awaiting)}"),
            anonymous.GetAsync($"/api/v1/invitations/{Token(accepted)}"));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NotFound);
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsByteArrayAsync()));
        bodies.Should().OnlyContain(body => body.SequenceEqual(bodies[0]));
    }

    [Test]
    public async Task An_existing_active_member_with_an_envelope_may_inspect_group_ciphertext()
    {
        var created = await CreateInvitation(ValidCreate());

        var response = await client.GetAsync($"/api/v1/invitations/{Token(created)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var inspected = (await response.Content.ReadFromJsonAsync<InspectInvitationContract>())!;
        inspected.NameCiphertext.Should().Be(Convert.ToBase64String([1]));
        inspected.NameNonce.Should().Be(Convert.ToBase64String([2]));
        inspected.ProtocolVersion.Should().Be(1);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("groupKeyEnvelope");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Open_and_bound_without_envelope_acceptance_waits_without_group_or_shared_access(bool bound)
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        var sharedRecordId = await SeedSharedRecord();
        var created = await CreateInvitation(new CreateInvitationRequest(
            groupId,
            bound ? member.Email : null,
            null,
            1));

        var response = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = (await response.Content.ReadFromJsonAsync<AcceptInvitationContract>())!;
        accepted.State.Should().Be(MembershipState.AwaitingOwnerApproval);
        accepted.RequiresApproval.Should().BeTrue();
        accepted.NameCiphertext.Should().BeNull();
        accepted.NameNonce.Should().BeNull();
        accepted.ProtocolVersion.Should().BeNull();
        accepted.GroupKeyEnvelope.Should().BeNull();
        accepted.EnvelopeProtocolVersion.Should().BeNull();
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await memberClient.GetAsync($"/api/v1/groups/{groupId}/members")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var sync = await memberClient.GetFromJsonAsync<SyncChangesContract>("/api/v1/sync/changes");
        sync!.Records.Should().NotContain(record => record.Id == sharedRecordId);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == groupId && membership.UserId == member.Id);
        membership.State.Should().Be(MembershipState.AwaitingOwnerApproval);
        membership.Role.Should().Be(MembershipRole.Member);
        membership.GroupKeyEnvelope.Should().BeNull();
        membership.EnvelopeProtocolVersion.Should().Be(0);
        membership.RevokedAt.Should().BeNull();
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id);
        invitation.State.Should().Be(InvitationState.AwaitingOwnerApproval);
        invitation.AcceptedByUserId.Should().Be(member.Id);
    }

    [Test]
    public async Task A_target_email_mismatch_is_neutral_and_writes_nothing()
    {
        using var intendedClient = await factory.CreateAuthenticatedClient();
        var intended = await CurrentIdentity(intendedClient);
        using var otherClient = await factory.CreateAuthenticatedClient();
        var other = await CurrentIdentity(otherClient);
        var created = await CreateInvitation(new CreateInvitationRequest(
            groupId,
            intended.Email,
            Convert.ToBase64String([131]),
            1));

        var mismatch = await otherClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));
        var unknown = await otherClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Xpense.API.Infrastructure.Invitations.InvitationTokenCodec.Generate().Token));

        mismatch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mismatch.Content.ReadAsByteArrayAsync()).Should().Equal(await unknown.Content.ReadAsByteArrayAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.GroupMemberships.AnyAsync(membership => membership.UserId == other.Id)).Should().BeFalse();
        (await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id))
            .State.Should().Be(InvitationState.Pending);
    }

    [Test]
    public async Task A_revoked_member_row_is_reused_and_clears_revocation_on_bound_acceptance()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        var membershipId = await SeedMembership(member.Id, MembershipState.Revoked, null);
        var envelope = Convert.ToBase64String([141, 142]);
        var created = await CreateInvitation(new CreateInvitationRequest(groupId, member.Email, envelope, 1));

        var response = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var membership = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership => membership.Id == membershipId);
        membership.State.Should().Be(MembershipState.Active);
        membership.Role.Should().Be(MembershipRole.Member);
        membership.RevokedAt.Should().BeNull();
        membership.GroupKeyEnvelope.Should().Equal([141, 142]);
        membership.EnvelopeProtocolVersion.Should().Be(1);
    }

    [Test]
    public async Task Same_user_replay_is_stable_conflict_while_another_user_gets_neutral_not_found()
    {
        using var firstClient = await factory.CreateAuthenticatedClient();
        using var otherClient = await factory.CreateAuthenticatedClient();
        var created = await CreateInvitation(ValidCreate());
        var request = new AcceptInvitationRequest(Token(created));

        var accepted = await firstClient.PostAsJsonAsync("/api/v1/invitations/accept", request);
        var replay = await firstClient.PostAsJsonAsync("/api/v1/invitations/accept", request);
        var other = await otherClient.PostAsJsonAsync("/api/v1/invitations/accept", request);

        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await replay.Content.ReadAsStringAsync()).Should().Contain("InvitationStateConflict");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Content.ReadAsStringAsync()).Should().Contain("This invitation is no longer valid.");
    }

    [Test]
    public async Task Concurrent_same_user_acceptance_has_one_success_and_one_stable_conflict()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var created = await CreateInvitation(ValidCreate());
        var request = new AcceptInvitationRequest(Token(created));

        var responses = await Task.WhenAll(
            memberClient.PostAsJsonAsync("/api/v1/invitations/accept", request),
            memberClient.PostAsJsonAsync("/api/v1/invitations/accept", request));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.OK);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Concurrent_different_users_have_one_success_and_one_neutral_loser()
    {
        using var firstClient = await factory.CreateAuthenticatedClient();
        var firstId = await CurrentUserId(firstClient);
        using var secondClient = await factory.CreateAuthenticatedClient();
        var secondId = await CurrentUserId(secondClient);
        var created = await CreateInvitation(ValidCreate());
        var request = new AcceptInvitationRequest(Token(created));

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/invitations/accept", request),
            secondClient.PostAsJsonAsync("/api/v1/invitations/accept", request));

        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.OK);
        responses.Should().ContainSingle(response => response.StatusCode == HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id);
        invitation.State.Should().Be(InvitationState.AwaitingOwnerApproval);
        new[] { firstId, secondId }.Should().Contain(invitation.AcceptedByUserId!.Value);
        var memberships = await dbContext.GroupMemberships
            .Where(membership => membership.GroupId == groupId &&
                (membership.UserId == firstId || membership.UserId == secondId))
            .ToArrayAsync();
        memberships.Should().ContainSingle(membership =>
            membership.UserId == invitation.AcceptedByUserId &&
            membership.State == MembershipState.AwaitingOwnerApproval);
    }

    [Test]
    public async Task Approval_requires_the_owner_exact_awaiting_state_and_canonical_envelope()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var awaiting = await CreateInvitation(ValidCreate());
        (await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(awaiting)))).StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = await CreateInvitation(ValidCreate());
        var valid = new ApproveInvitationRequest(Convert.ToBase64String([151]), 1);

        var wrongOwner = await memberClient.PostAsJsonAsync(
            $"/api/v1/invitations/{awaiting.Id}/approve",
            valid);
        var wrongState = await client.PostAsJsonAsync(
            $"/api/v1/invitations/{pending.Id}/approve",
            valid);
        var missing = await client.PostAsJsonAsync(
            $"/api/v1/invitations/{Guid.CreateVersion7()}/approve",
            valid);

        await AssertIdenticalNotFound(wrongOwner, wrongState, missing);
        var invalid = new[]
        {
            valid with { GroupKeyEnvelope = string.Empty },
            valid with { GroupKeyEnvelope = "not-base64" },
            valid with { GroupKeyEnvelope = "AR==" },
            valid with { GroupKeyEnvelope = Convert.ToBase64String(new byte[4097]) },
            valid with { ProtocolVersion = 0 },
            valid with { ProtocolVersion = 2 }
        };
        foreach (var request in invalid)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/invitations/{awaiting.Id}/approve",
                request);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == awaiting.Id))
            .State.Should().Be(InvitationState.AwaitingOwnerApproval);
    }

    [Test]
    public async Task Accept_and_approval_save_failures_are_atomic()
    {
        await using var failingAcceptFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<GroupMembership>());
        using var failingMemberClient = await failingAcceptFactory.CreateAuthenticatedClient();
        var failingMember = await CurrentIdentity(failingMemberClient);
        var bound = await CreateInvitation(new CreateInvitationRequest(
            groupId,
            failingMember.Email,
            Convert.ToBase64String([171]),
            1));

        var failedAccept = await failingMemberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(bound)));

        failedAccept.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using (var verifyScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            (await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == bound.Id))
                .State.Should().Be(InvitationState.Pending);
            (await dbContext.GroupMemberships.AnyAsync(membership => membership.UserId == failingMember.Id))
                .Should().BeFalse();
        }

        using var awaitingMemberClient = await factory.CreateAuthenticatedClient();
        var open = await CreateInvitation(ValidCreate());
        (await awaitingMemberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(open)))).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var failingApprovalFactory = new WebApiTestFactory(
            connectionString,
            new FailOnSaveInterceptor<GroupMembership>()).AsUser(ownerId);
        using var failingOwnerClient = failingApprovalFactory.CreateClient();

        var failedApproval = await failingOwnerClient.PostAsJsonAsync(
            $"/api/v1/invitations/{open.Id}/approve",
            new ApproveInvitationRequest(Convert.ToBase64String([172]), 1));

        failedApproval.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await finalDbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == open.Id))
            .State.Should().Be(InvitationState.AwaitingOwnerApproval);
        var awaitingUserId = await CurrentUserId(awaitingMemberClient);
        var membership = await finalDbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == groupId && membership.UserId == awaitingUserId);
        membership.State.Should().Be(MembershipState.AwaitingOwnerApproval);
        membership.GroupKeyEnvelope.Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Accept_racing_revoke_or_group_delete_has_only_serialized_outcomes(bool deleteGroup)
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var member = await CurrentIdentity(memberClient);
        var created = await CreateInvitation(new CreateInvitationRequest(
            groupId,
            member.Email,
            Convert.ToBase64String([181]),
            1));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("key", groupId.ToString("N"));
            await command.ExecuteNonQueryAsync();
        }
        var attempts = lockInterceptor.WaitForAttempts(2);
        var acceptTask = memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));
        var competingTask = deleteGroup
            ? client.DeleteAsync($"/api/v1/groups/{groupId}")
            : client.DeleteAsync($"/api/v1/invitations/{created.Id}");
        await attempts.WaitAsync(TimeSpan.FromSeconds(5));

        await transaction.CommitAsync();
        await Task.WhenAll(acceptTask, competingTask);

        if (deleteGroup)
        {
            (acceptTask.Result.StatusCode, competingTask.Result.StatusCode).Should().BeOneOf(
                (HttpStatusCode.OK, HttpStatusCode.BadRequest),
                (HttpStatusCode.NotFound, HttpStatusCode.NoContent));
        }
        else
        {
            (acceptTask.Result.StatusCode, competingTask.Result.StatusCode).Should().BeOneOf(
                (HttpStatusCode.OK, HttpStatusCode.NotFound),
                (HttpStatusCode.NotFound, HttpStatusCode.NoContent));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id);
        var membership = await dbContext.GroupMemberships.SingleOrDefaultAsync(membership =>
            membership.GroupId == groupId && membership.UserId == member.Id);
        if (acceptTask.Result.StatusCode == HttpStatusCode.OK)
        {
            invitation.State.Should().Be(InvitationState.Accepted);
            membership!.State.Should().Be(MembershipState.Active);
        }
        else
        {
            membership.Should().BeNull();
            if (!deleteGroup)
                invitation.State.Should().Be(InvitationState.Revoked);
        }
    }

    [Test]
    public async Task Approval_racing_member_removal_never_leaves_an_awaiting_member_with_an_envelope()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var created = await CreateInvitation(ValidCreate());
        (await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)))).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("key", groupId.ToString("N"));
            await command.ExecuteNonQueryAsync();
        }
        var attempts = lockInterceptor.WaitForAttempts(2);
        var approveTask = client.PostAsJsonAsync(
            $"/api/v1/invitations/{created.Id}/approve",
            new ApproveInvitationRequest(Convert.ToBase64String([191]), 1));
        var removeTask = client.DeleteAsync($"/api/v1/groups/{groupId}/members/{memberId}");
        await attempts.WaitAsync(TimeSpan.FromSeconds(5));

        await transaction.CommitAsync();
        await Task.WhenAll(approveTask, removeTask);

        (approveTask.Result.StatusCode, removeTask.Result.StatusCode).Should().BeOneOf(
            (HttpStatusCode.NoContent, HttpStatusCode.OK),
            (HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        await using var scope = factory.Services.CreateAsyncScope();
        var membership = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .GroupMemberships.SingleAsync(membership =>
                membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().BeOneOf(MembershipState.Active, MembershipState.Revoked);
        membership.State.Should().NotBe(MembershipState.AwaitingOwnerApproval);
        if (membership.State == MembershipState.Active)
            membership.GroupKeyEnvelope.Should().Equal([191]);
        else
            membership.GroupKeyEnvelope.Should().BeNull();
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
        using var awaitingClient = await factory.CreateAuthenticatedClient();
        await AddMembership(groupId, memberId);
        var awaitingInvitation = await CreateInvitation(ValidCreate());
        (await awaitingClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(awaitingInvitation)))).StatusCode.Should().Be(HttpStatusCode.OK);
        var awaitingId = awaitingInvitation.Id;
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
    public async Task Revoking_an_awaiting_invitation_atomically_revokes_the_awaiting_membership_and_replay_is_neutral()
    {
        using var memberClient = await factory.CreateAuthenticatedClient();
        var memberId = await CurrentUserId(memberClient);
        var created = await CreateInvitation(ValidCreate());
        (await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)))).StatusCode.Should().Be(HttpStatusCode.OK);

        var revoked = await client.DeleteAsync($"/api/v1/invitations/{created.Id}");
        var replay = await memberClient.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new AcceptInvitationRequest(Token(created)));

        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        replay.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await replay.Content.ReadAsStringAsync()).Should().Contain("This invitation is no longer valid.");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == created.Id);
        invitation.State.Should().Be(InvitationState.Revoked);
        invitation.AcceptedByUserId.Should().Be(memberId);
        var membership = await dbContext.GroupMemberships.SingleAsync(membership =>
            membership.GroupId == groupId && membership.UserId == memberId);
        membership.State.Should().Be(MembershipState.Revoked);
        membership.GroupKeyEnvelope.Should().BeNull();
        membership.RevokedAt.Should().NotBeNull();
        membership.UpdatedAt.Should().Be(membership.RevokedAt!.Value);
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
            await anonymous.DeleteAsync($"/api/v1/invitations/{Guid.CreateVersion7()}"),
            await anonymous.PostAsJsonAsync(
                "/api/v1/invitations/accept",
                new AcceptInvitationRequest("unknown")),
            await anonymous.PostAsJsonAsync(
                $"/api/v1/invitations/{Guid.CreateVersion7()}/approve",
                new ApproveInvitationRequest(Convert.ToBase64String([1]), 1)),
            await anonymous.GetAsync(
                $"/api/v1/invitations/{Guid.CreateVersion7()}/approval-context")
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

    private async Task<CreatedInvitationContract> CreateInvitation(CreateInvitationRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/v1/invitations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedInvitationContract>())!;
    }

    private static string Token(CreatedInvitationContract invitation) =>
        invitation.InvitationLink.Split('/').Last();

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

    private async Task SeedEncryptionIdentity(
        Guid userId,
        byte[] publicKey,
        byte[] encryptedPrivateKey,
        byte[] nonce,
        int protocolVersion)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.UserEncryptionIdentities.Add(new UserEncryptionIdentity
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PublicKey = publicKey,
            EncryptedPrivateKey = encryptedPrivateKey,
            Nonce = nonce,
            ProtocolVersion = protocolVersion,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task UpdateInvitation(Guid invitationId, Action<GroupInvitation> update)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var invitation = await dbContext.GroupInvitations.SingleAsync(invitation => invitation.Id == invitationId);
        update(invitation);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedMembership(
        Guid userId,
        MembershipState state,
        byte[]? envelope)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        var membership = new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            UserId = userId,
            Role = MembershipRole.Member,
            State = state,
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = envelope is null ? 0 : 1,
            CreatedAt = now,
            UpdatedAt = now,
            RevokedAt = state == MembershipState.Revoked ? now : null
        };
        dbContext.GroupMemberships.Add(membership);
        await dbContext.SaveChangesAsync();
        return membership.Id;
    }

    private async Task<Guid> SeedSharedRecord()
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
            OwnerUserId = ownerId,
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
            GrantedByUserId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.EncryptedRecords.Add(new EncryptedRecord
        {
            Id = recordId,
            RecordType = EncryptedRecordType.Account,
            OwnerUserId = ownerId,
            ParentResourceId = resourceId,
            Revision = 1,
            Payload = [1, 2, 3],
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync();
        return recordId;
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
        return (await CurrentIdentity(actor)).Id;
    }

    private static async Task<IdentityContract> CurrentIdentity(HttpClient actor) =>
        (await actor.GetFromJsonAsync<IdentityContract>("/api/v1/auth/me"))!;

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

    private sealed record InspectInvitationContract(
        InvitationState State,
        bool RequiresApproval,
        string InviterEmail,
        string? NameCiphertext,
        string? NameNonce,
        int? ProtocolVersion);

    private sealed record AcceptInvitationRequest(string Token);

    private sealed record AcceptInvitationContract(
        Guid GroupId,
        MembershipState State,
        bool RequiresApproval,
        string? NameCiphertext,
        string? NameNonce,
        int? ProtocolVersion,
        string? GroupKeyEnvelope,
        int? EnvelopeProtocolVersion);

    private sealed record ApproveInvitationRequest(string GroupKeyEnvelope, int ProtocolVersion);

    private sealed record ApprovalContextContract(
        Guid InvitationId,
        Guid AcceptedUserId,
        string PublicKey,
        int ProtocolVersion);

    private sealed record IdentityContract(Guid Id, string Email);

    private sealed record SyncChangesContract(SyncRecordContract[] Records);

    private sealed record SyncRecordContract(Guid Id);

    private sealed class GroupLockAttemptInterceptor : DbCommandInterceptor
    {
        private TaskCompletionSource? expectedAttempts;
        private int remainingAttempts;

        public Task WaitForNextAttempt() => WaitForAttempts(1);

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
