namespace Xpense.Domain.Entities;

public sealed class ClaimToken
{
    public const string LegacyPurpose = "legacy-claim-v1";

    public Guid Id { get; set; }

    public string Purpose { get; set; } = LegacyPurpose;

    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTime ExpiresAt { get; set; }

    public DateTime? DatasetDownloadedAt { get; set; }

    public int? ExpectedRecordCount { get; set; }

    public string? ExpectedTypeCounts { get; set; }

    public byte[]? ExpectedManifestHash { get; set; }

    public byte[]? ExpectedSourceContentHash { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void Rotate(byte[] tokenHash, DateTime now)
    {
        TokenHash = tokenHash;
        ExpiresAt = now.AddMinutes(30);
        DatasetDownloadedAt = null;
        ExpectedRecordCount = null;
        ExpectedTypeCounts = null;
        ExpectedManifestHash = null;
        ExpectedSourceContentHash = null;
        UpdatedAt = now;
    }
}
