using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Xpense.Notifications.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ISmtpTransport smtpTransport) : IEmailSender
{
    private readonly EmailOptions options = options.Value;

    public bool IsEnabled => options.Enabled;

    public async Task SendInvitation(
        Guid deliveryId,
        string emailAddress,
        string invitationLink,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage(options.FromAddress, emailAddress)
        {
            Subject = "Your Xpense invitation",
            Body = $"You have been invited to Xpense.\n\n{invitationLink}\n",
            IsBodyHtml = false
        };
        var host = new MailAddress(options.FromAddress).Host;
        message.Headers.Add("Message-Id", $"<xpense-invitation-{deliveryId:N}@{host}>");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            await smtpTransport.Send(message, options, deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Email delivery timed out.");
        }
    }
}
