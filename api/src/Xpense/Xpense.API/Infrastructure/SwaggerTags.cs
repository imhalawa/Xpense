using System;
using System.Linq;

namespace Xpense.API.Infrastructure;

public static class SwaggerTags
{
    private const string VersionedPrefix = "api/v1/";

    private const string Fallback = "General";

    public static string ForRoute(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Fallback;

        var withoutPrefix = relativePath.StartsWith(VersionedPrefix, StringComparison.OrdinalIgnoreCase)
            ? relativePath[VersionedPrefix.Length..]
            : relativePath;

        var resource = withoutPrefix
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(segment => !segment.StartsWith('{'));

        if (string.IsNullOrWhiteSpace(resource))
            return Fallback;

        return string.Concat(char.ToUpperInvariant(resource[0]), resource[1..]);
    }
}
