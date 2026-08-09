using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Notifications.Email;
using Xpense.Persistence;

namespace Xpense.Notifications.Rules;

public sealed class InvitationEmailRule(
    XpenseDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    IEmailSender emailSender) : INotificationRule<GroupInvitationCreated>
{
    private const string ProtectorPurpose = "Xpense.InvitationDelivery";

    public async Task<IReadOnlyList<NotificationDraft>> Evaluate(
        Event<GroupInvitationCreated> @event,
        CancellationToken cancellationToken)
    {
        var delivery = await dbContext.InvitationDeliveries
            .SingleOrDefaultAsync(item => item.InvitationId == @event.Body.InvitationId, cancellationToken);

        if (delivery is null || delivery.Status is DeliveryStatus.Sent or DeliveryStatus.Disabled or DeliveryStatus.Failed)
            return [];

        var now = DateTime.UtcNow;
        var deliverable = await dbContext.GroupInvitations
            .AnyAsync(invitation => invitation.Id == delivery.InvitationId
                                    && invitation.State == InvitationState.Pending
                                    && invitation.ExpiresAt > now
                                    && dbContext.Groups.Any(group => group.Id == invitation.GroupId && !group.IsDeleted),
                cancellationToken);

        if (!deliverable || !emailSender.IsEnabled)
        {
            delivery.Status = DeliveryStatus.Disabled;
            delivery.ProtectedPayload = null;
            delivery.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return [];
        }

        string invitationLink;
        try
        {
            invitationLink = dataProtectionProvider.CreateProtector(ProtectorPurpose)
                .Unprotect(delivery.ProtectedPayload ?? string.Empty);
            await emailSender.SendInvitation(delivery.Id, delivery.EmailAddress, invitationLink, cancellationToken);
        }
        catch
        {
            throw new InvitationEmailDeliveryException();
        }

        delivery.Attempts++;
        delivery.Status = DeliveryStatus.Sent;
        delivery.SentAt = now;
        delivery.UpdatedAt = now;
        delivery.LastError = null;
        delivery.ProtectedPayload = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return [];
    }
}

public sealed class InvitationEmailDeliveryException : Exception
{
    public InvitationEmailDeliveryException() : base("Invitation email delivery failed.")
    {
    }
}

public sealed class InvitationEmailFailureHandler(XpenseDbContext dbContext)
    : IEventFailureHandler<GroupInvitationCreated>
{
    private const string SafeError = "Invitation email delivery failed.";

    public async Task Handle(
        Event<GroupInvitationCreated> @event,
        Exception exception,
        int attempt,
        CancellationToken cancellationToken)
    {
        if (exception is not InvitationEmailDeliveryException)
            return;

        var delivery = await dbContext.InvitationDeliveries
            .SingleOrDefaultAsync(item => item.InvitationId == @event.Body.InvitationId, cancellationToken);

        if (delivery is null || delivery.Status != DeliveryStatus.Pending)
            return;

        delivery.Attempts++;
        delivery.LastError = SafeError;
        delivery.UpdatedAt = DateTime.UtcNow;

        if (attempt >= EventRecord.MaxAttempts)
        {
            delivery.Status = DeliveryStatus.Failed;
            delivery.ProtectedPayload = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
