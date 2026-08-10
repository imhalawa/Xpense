namespace Xpense.API.Infrastructure.Authentication;

public sealed class XpenseForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";

    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownIPNetworks { get; set; } = [];
}
