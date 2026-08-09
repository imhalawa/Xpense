using FluentAssertions;
using Xpense.Notifications.Email;

namespace Xpense.Tests.Unit;

[TestFixture]
public class EmailOptionsValidatorTests
{
    [Test]
    public void Disabled_email_needs_no_transport_configuration()
    {
        new EmailOptionsValidator().Validate(null, new EmailOptions()).Succeeded.Should().BeTrue();
    }

    [TestCase("", 587, "sender@example.test", 10, null, null)]
    [TestCase("smtp.example.test", 0, "sender@example.test", 10, null, null)]
    [TestCase("smtp.example.test", 587, "invalid", 10, null, null)]
    [TestCase("smtp.example.test", 587, "", 10, null, null)]
    [TestCase("smtp.example.test", 587, null, 10, null, null)]
    [TestCase("smtp.example.test", 587, "sender@example.test", 31, null, null)]
    [TestCase("smtp.example.test", 587, "sender@example.test", 10, "user", null)]
    public void Enabled_email_rejects_incomplete_or_unsafe_configuration(
        string host,
        int port,
        string? fromAddress,
        int timeoutSeconds,
        string? username,
        string? password)
    {
        var result = new EmailOptionsValidator().Validate(null, new EmailOptions
        {
            Enabled = true,
            Host = host,
            Port = port,
            FromAddress = fromAddress!,
            TimeoutSeconds = timeoutSeconds,
            Username = username,
            Password = password
        });

        result.Failed.Should().BeTrue();
    }

    [Test]
    public void Enabled_email_accepts_bounded_tls_configuration_with_paired_credentials()
    {
        var result = new EmailOptionsValidator().Validate(null, new EmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            Port = 587,
            FromAddress = "sender@example.test",
            UseTls = true,
            TimeoutSeconds = 10,
            Username = "user",
            Password = "secret"
        });

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void Enabled_email_accepts_both_credentials_as_blank()
    {
        var result = new EmailOptionsValidator().Validate(null, new EmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            Port = 587,
            FromAddress = "sender@example.test",
            TimeoutSeconds = 10,
            Username = " ",
            Password = " "
        });

        result.Succeeded.Should().BeTrue();
    }
}
