namespace Xpense.Domain.Entities;

public class RecordEnvelope
{
    public Guid Id { get; set; }

    public Guid EncryptedRecordId { get; set; }

    public Guid? GroupId { get; set; }

    public byte[] WrappedKey { get; set; } = [];

    public byte[] Nonce { get; set; } = [];

    public byte[]? EncapsulatedKey { get; set; }

    public int ProtocolVersion { get; set; }
}
