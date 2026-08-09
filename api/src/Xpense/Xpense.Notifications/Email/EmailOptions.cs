namespace Xpense.Notifications.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string FromAddress { get; set; } = string.Empty;

    public bool UseTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}
