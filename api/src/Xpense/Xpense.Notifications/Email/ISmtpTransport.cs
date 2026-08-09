using System.Net;
using System.Net.Mail;

namespace Xpense.Notifications.Email;

public interface ISmtpTransport
{
    Task Send(MailMessage message, EmailOptions options, CancellationToken cancellationToken);
}

public sealed class SmtpTransport : ISmtpTransport
{
    public async Task Send(MailMessage message, EmailOptions options, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseTls
        };

        if (!string.IsNullOrWhiteSpace(options.Username))
            client.Credentials = new NetworkCredential(options.Username, options.Password);

        await client.SendMailAsync(message, cancellationToken);
    }
}
