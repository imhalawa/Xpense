using System;

namespace Xpense.API.Infrastructure.LegacyClaim;

public sealed class LegacyClaimOptions
{
    public const string SectionName = "LegacyClaim";
    public const string LegacyDataMode = "legacy";
    public const string EncryptedDataMode = "encrypted";

    public bool Enabled { get; set; }

    public string? DesignatedUserId { get; set; }

    public string DataMode { get; set; } = LegacyDataMode;

    public bool IsEncrypted => string.Equals(DataMode, EncryptedDataMode, StringComparison.OrdinalIgnoreCase);

    public bool HasKnownDataMode =>
        string.Equals(DataMode, LegacyDataMode, StringComparison.OrdinalIgnoreCase) || IsEncrypted;

    public bool TryGetDesignatedUserId(out Guid userId) =>
        Guid.TryParse(DesignatedUserId, out userId) && userId != Guid.Empty;
}
