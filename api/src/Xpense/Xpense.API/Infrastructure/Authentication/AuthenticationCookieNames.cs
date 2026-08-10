namespace Xpense.API.Infrastructure.Authentication;

public static class AuthenticationCookieNames
{
    public const string Session = "xpense.session";
    public const string Antiforgery = "xpense.antiforgery";
    public const string AntiforgeryHeader = "X-Xpense-Antiforgery";
}
