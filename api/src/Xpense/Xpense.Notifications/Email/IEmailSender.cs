namespace Xpense.Notifications.Email;

public interface IEmailSender
{
    bool IsEnabled { get; }

    Task SendInvitation(Guid deliveryId, string emailAddress, string invitationLink, CancellationToken cancellationToken);
}
