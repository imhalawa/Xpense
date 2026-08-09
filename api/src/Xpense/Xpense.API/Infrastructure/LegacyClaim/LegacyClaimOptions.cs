using System;

namespace Xpense.API.Infrastructure.LegacyClaim;

public sealed class LegacyClaimOptions
{
    public const string SectionName = "LegacyClaim";

    public bool Enabled { get; set; }

    public string? DesignatedUserId { get; set; }

    public bool TryGetDesignatedUserId(out Guid userId) =>
        Guid.TryParse(DesignatedUserId, out userId) && userId != Guid.Empty;
}
