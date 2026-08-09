using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Xpense.Notifications.Email;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(options.Host))
            return ValidateOptionsResult.Fail("The email host is required when email delivery is enabled.");
        if (options.Port is <= 0 or > 65535)
            return ValidateOptionsResult.Fail("The email port must be valid when email delivery is enabled.");
        if (!IsEmailAddress(options.FromAddress))
            return ValidateOptionsResult.Fail("The email from address must be valid when email delivery is enabled.");
        if (options.TimeoutSeconds is <= 0 or > 30)
            return ValidateOptionsResult.Fail("The email timeout must be between 1 and 30 seconds.");
        if (string.IsNullOrWhiteSpace(options.Username) != string.IsNullOrWhiteSpace(options.Password))
            return ValidateOptionsResult.Fail("The email username and password must be supplied together.");
        return ValidateOptionsResult.Success;
    }

    private static bool IsEmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
