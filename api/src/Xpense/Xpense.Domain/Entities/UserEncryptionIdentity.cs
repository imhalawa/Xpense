namespace Xpense.Domain.Entities;

public class UserEncryptionIdentity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] PublicKey { get; set; } = [];

    public byte[] EncryptedPrivateKey { get; set; } = [];

    public byte[] Nonce { get; set; } = [];

    public int ProtocolVersion { get; set; }

    public DateTime CreatedAt { get; set; }
}
