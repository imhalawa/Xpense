namespace Xpense.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public byte[] NameCiphertext { get; set; } = [];

    public byte[] NameNonce { get; set; } = [];

    public int ProtocolVersion { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void Rename(byte[] nameCiphertext, byte[] nameNonce, int protocolVersion)
    {
        NameCiphertext = nameCiphertext;
        NameNonce = nameNonce;
        ProtocolVersion = protocolVersion;
        Touch();
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
        Touch();
    }

    public void TransferOwnership(Guid ownerUserId, DateTime now)
    {
        OwnerUserId = ownerUserId;
        UpdatedAt = now;
    }

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}
