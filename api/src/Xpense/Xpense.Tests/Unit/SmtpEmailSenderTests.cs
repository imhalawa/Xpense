using FluentAssertions;
using Microsoft.Extensions.Options;
using Xpense.Notifications.Email;

namespace Xpense.Tests.Unit;

[TestFixture]
public class SmtpEmailSenderTests
{
    [Test]
    public async Task Configured_deadline_cancels_async_transport_and_reports_a_safe_timeout()
    {
        var transport = new BlockingSmtpTransport();
        var sender = new SmtpEmailSender(Options.Create(ValidOptions(timeoutSeconds: 1)), transport);

        var action = () => sender.SendInvitation(
            Guid.CreateVersion7(),
            "invitee@example.test",
            "https://xpense.example/invitations/token",
            CancellationToken.None);

        await action.Should().ThrowAsync<TimeoutException>()
            .WithMessage("Email delivery timed out.");
        transport.ObservedCancellation.Should().BeTrue();
    }

    [Test]
    public async Task Caller_cancellation_remains_an_operation_canceled_with_the_original_token()
    {
        var transport = new BlockingSmtpTransport();
        var sender = new SmtpEmailSender(Options.Create(ValidOptions(timeoutSeconds: 30)), transport);
        using var cancellation = new CancellationTokenSource();

        var sending = sender.SendInvitation(
            Guid.CreateVersion7(),
            "invitee@example.test",
            "https://xpense.example/invitations/token",
            cancellation.Token);
        await transport.Started.Task;
        cancellation.Cancel();

        var exception = await FluentActions.Awaiting(() => sending).Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cancellation.Token);
    }

    [Test]
    public void Notifications_container_uses_the_aspnet_runtime_required_by_its_framework_reference()
    {
        var dockerfile = Path.GetFullPath(
            "../../../../../../docker/notifications/Dockerfile",
            AppContext.BaseDirectory);

        File.ReadAllText(dockerfile).Should().Contain(
            "FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final");
    }

    private static EmailOptions ValidOptions(int timeoutSeconds) => new()
    {
        Enabled = true,
        Host = "smtp.example.test",
        Port = 587,
        FromAddress = "sender@example.test",
        UseTls = true,
        TimeoutSeconds = timeoutSeconds
    };

    private sealed class BlockingSmtpTransport : ISmtpTransport
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObservedCancellation { get; private set; }

        public async Task Send(
            System.Net.Mail.MailMessage message,
            EmailOptions options,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }
}
