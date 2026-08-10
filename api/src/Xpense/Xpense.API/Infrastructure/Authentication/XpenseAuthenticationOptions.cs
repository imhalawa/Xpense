namespace Xpense.API.Infrastructure.Authentication;

public sealed class XpenseAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string RelyingPartyDomain { get; set; } = "localhost";

    public string RelyingPartyName { get; set; } = "Xpense";

    public string[] AllowedOrigins { get; set; } = [];

    public RegistrationPolicy Registration { get; set; } = RegistrationPolicy.Open;

    public string PublicUrl { get; set; } = "http://localhost:5173";
}

public enum RegistrationPolicy
{
    Open,
    InviteOnly,
    Closed
}
