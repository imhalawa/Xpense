using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class VaultWrapper
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public VaultWrapperKind Kind { get; set; }

    public byte[]? CredentialId { get; set; }

    public byte[] Salt { get; set; } = [];

    public byte[] Ciphertext { get; set; } = [];

    public byte[] Nonce { get; set; } = [];

    public string? Parameters { get; set; }

    public byte[]? AuthenticationTokenHash { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public int ProtocolVersion { get; set; }

    public string? Label { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
