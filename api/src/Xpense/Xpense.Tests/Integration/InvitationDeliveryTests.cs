using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Notifications;
using Xpense.Notifications.Email;
using Xpense.Notifications.Rules;
using Xpense.Persistence;

namespace Xpense.Tests.Integration;

[TestFixture]
public class InvitationDeliveryTests
{
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp() => connectionString = await PostgresFixture.CreateDatabase();

    [Test]
    public async Task Pending_delivery_sends_exact_protected_link_once_and_clears_payload()
    {
        var sender = new RecordingEmailSender();
        var seeded = await Seed(sender.Provider, DeliveryStatus.Pending);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.Attempts.Should().Be(1);
        delivery.SentAt.Should().NotBeNull();
        delivery.ProtectedPayload.Should().BeNull();
        delivery.LastError.Should().BeNull();
        sender.Messages.Should().ContainSingle().Which.Should().Be((seeded.EmailAddress, seeded.Link));
        (await dbContext.Notifications.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Disabled_adapter_disables_delivery_without_sending()
    {
        var sender = new RecordingEmailSender { IsEnabled = false };
        await Seed(sender.Provider, DeliveryStatus.Pending);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Disabled);
        delivery.ProtectedPayload.Should().BeNull();
        sender.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task Transient_failure_persists_only_safe_retry_metadata()
    {
        var sender = new RecordingEmailSender { Failure = new InvalidOperationException("smtp secret") };
        await Seed(sender.Provider, DeliveryStatus.Pending);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.Attempts.Should().Be(1);
        delivery.LastError.Should().Be("Invitation email delivery failed.");
        delivery.LastError.Should().NotContain("smtp secret");
        delivery.ProtectedPayload.Should().NotBeNull();
        var record = await dbContext.Events.SingleAsync();
        record.Attempts.Should().Be(1);
        record.LastError.Should().NotContain("smtp secret");
    }

    [Test]
    public async Task Final_failure_deadletters_delivery_and_clears_payload()
    {
        var sender = new RecordingEmailSender { Failure = new InvalidOperationException("unsafe") };
        await Seed(sender.Provider, DeliveryStatus.Pending, EventRecord.MaxAttempts - 1);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.Attempts.Should().Be(EventRecord.MaxAttempts);
        delivery.ProtectedPayload.Should().BeNull();
        (await dbContext.Events.SingleAsync()).ProcessedAt.Should().NotBeNull();
    }

    [Test]
    public async Task A_transient_failure_retries_then_succeeds_and_clears_the_error()
    {
        var sender = new RecordingEmailSender { Failure = new InvalidOperationException("unsafe") };
        await Seed(sender.Provider, DeliveryStatus.Pending);
        await Process(sender);

        sender.Failure = null;
        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Sent);
        delivery.Attempts.Should().Be(2);
        delivery.LastError.Should().BeNull();
        sender.Messages.Should().ContainSingle();
    }

    [Test]
    public async Task A_terminal_delivery_is_idempotent_when_its_event_is_replayed()
    {
        var sender = new RecordingEmailSender();
        await Seed(sender.Provider, DeliveryStatus.Sent);

        await Process(sender);

        sender.Messages.Should().BeEmpty();
        await using var dbContext = NewDbContext();
        (await dbContext.InvitationDeliveries.SingleAsync()).Status.Should().Be(DeliveryStatus.Sent);
        (await dbContext.Events.SingleAsync()).ProcessedAt.Should().NotBeNull();
    }

    [TestCase(InvitationState.Revoked, false, false)]
    [TestCase(InvitationState.Accepted, false, false)]
    [TestCase(InvitationState.AwaitingOwnerApproval, false, false)]
    [TestCase(InvitationState.Pending, true, false)]
    [TestCase(InvitationState.Pending, false, true)]
    public async Task An_ineligible_invitation_is_disabled_without_sending(
        InvitationState state,
        bool expired,
        bool deletedGroup)
    {
        var sender = new RecordingEmailSender();
        await Seed(sender.Provider, DeliveryStatus.Pending, invitationState: state, expired: expired, deletedGroup: deletedGroup);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Disabled);
        delivery.ProtectedPayload.Should().BeNull();
        sender.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task An_open_invitation_event_without_a_delivery_is_a_successful_no_op()
    {
        var sender = new RecordingEmailSender();
        await Seed(sender.Provider, DeliveryStatus.Pending);
        await using (var dbContext = NewDbContext())
        {
            dbContext.InvitationDeliveries.Remove(await dbContext.InvitationDeliveries.SingleAsync());
            await dbContext.SaveChangesAsync();
        }

        await Process(sender);

        sender.Messages.Should().BeEmpty();
        await using var verify = NewDbContext();
        (await verify.Events.SingleAsync()).ProcessedAt.Should().NotBeNull();
    }

    [Test]
    public async Task A_payload_protected_with_another_key_fails_without_leaking_key_details()
    {
        var sender = new RecordingEmailSender();
        await Seed(new EphemeralDataProtectionProvider(), DeliveryStatus.Pending);

        await Process(sender);

        await using var dbContext = NewDbContext();
        var delivery = await dbContext.InvitationDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.LastError.Should().Be("Invitation email delivery failed.");
        sender.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task Concurrent_processors_claim_one_event_and_send_once()
    {
        var sender = new RecordingEmailSender();
        await Seed(sender.Provider, DeliveryStatus.Pending);

        await Task.WhenAll(Process(sender), Process(sender));

        sender.Messages.Should().ContainSingle();
        await using var dbContext = NewDbContext();
        (await dbContext.InvitationDeliveries.SingleAsync()).Status.Should().Be(DeliveryStatus.Sent);
    }

    [Test]
    public void Api_and_worker_providers_with_the_same_key_directory_and_application_name_share_payloads()
    {
        var directory = Directory.CreateTempSubdirectory("xpense-delivery-keys-");
        try
        {
            using var apiServices = new ServiceCollection()
                .AddDataProtection()
                .SetApplicationName("Xpense")
                .PersistKeysToFileSystem(directory)
                .Services.BuildServiceProvider();
            using var workerServices = new ServiceCollection()
                .AddDataProtection()
                .SetApplicationName("Xpense")
                .PersistKeysToFileSystem(directory)
                .Services.BuildServiceProvider();
            var protectedLink = apiServices.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Xpense.InvitationDelivery")
                .Protect("https://xpense.example/invitations/token");

            workerServices.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Xpense.InvitationDelivery")
                .Unprotect(protectedLink)
                .Should().Be("https://xpense.example/invitations/token");
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private XpenseDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<XpenseDbContext>().UseNpgsql(connectionString).Options);

    private async Task Process(RecordingEmailSender sender)
    {
        await using var dbContext = NewDbContext();
        IEventDispatcher dispatcher = new EventDispatcher<GroupInvitationCreated>(
            [new InvitationEmailRule(dbContext, sender.Provider, sender)]);
        IEventFailureDispatcher failureDispatcher = new EventFailureDispatcher<GroupInvitationCreated>(
            [new InvitationEmailFailureHandler(dbContext)]);
        var processor = new EventProcessor(
            dbContext,
            [dispatcher],
            NullLogger<EventProcessor>.Instance,
            [failureDispatcher]);
        await processor.ProcessBatch();
    }

    private async Task<(string EmailAddress, string Link)> Seed(
        IDataProtectionProvider provider,
        DeliveryStatus status,
        int eventAttempts = 0,
        InvitationState invitationState = InvitationState.Pending,
        bool expired = false,
        bool deletedGroup = false)
    {
        var now = DateTime.UtcNow;
        var userId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var invitationId = Guid.CreateVersion7();
        const string emailAddress = "invitee@example.test";
        const string link = "https://xpense.example/invitations/token";

        await using var dbContext = NewDbContext();
        dbContext.Users.Add(new XpenseUser
        {
            Id = userId,
            UserName = "owner@example.test",
            NormalizedUserName = "OWNER@EXAMPLE.TEST",
            Email = "owner@example.test",
            NormalizedEmail = "OWNER@EXAMPLE.TEST",
            State = AccountState.Active,
            CreatedAt = now
        });
        dbContext.Groups.Add(new Group
        {
            Id = groupId,
            OwnerUserId = userId,
            NameCiphertext = [1],
            NameNonce = [2],
            ProtocolVersion = 1,
            IsDeleted = deletedGroup,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.GroupInvitations.Add(new GroupInvitation
        {
            Id = invitationId,
            GroupId = groupId,
            InvitedByUserId = userId,
            TokenHash = new byte[32],
            State = invitationState,
            ExpiresAt = expired ? now.AddMinutes(-1) : now.AddDays(7),
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.InvitationDeliveries.Add(new InvitationDelivery
        {
            Id = Guid.CreateVersion7(),
            InvitationId = invitationId,
            EmailAddress = emailAddress,
            ProtectedPayload = provider.CreateProtector("Xpense.InvitationDelivery").Protect(link),
            Status = status,
            Attempts = eventAttempts,
            CreatedAt = now,
            UpdatedAt = now
        });
        var @event = Event.Of(new GroupInvitationCreated(invitationId), now);
        await new EventBus(dbContext).Emit(@event);
        await dbContext.SaveChangesAsync();
        var record = await dbContext.Events.SingleAsync();
        record.Attempts = eventAttempts;
        await dbContext.SaveChangesAsync();
        return (emailAddress, link);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public IDataProtectionProvider Provider { get; } = new EphemeralDataProtectionProvider();

        public bool IsEnabled { get; set; } = true;

        public Exception? Failure { get; set; }

        public List<(string EmailAddress, string Link)> Messages { get; } = [];

        public Task SendInvitation(
            Guid deliveryId,
            string emailAddress,
            string invitationLink,
            CancellationToken cancellationToken)
        {
            if (Failure is not null)
                throw Failure;
            Messages.Add((emailAddress, invitationLink));
            return Task.CompletedTask;
        }
    }
}
