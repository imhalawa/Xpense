using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class EncryptedRecord
{
    public Guid Id { get; set; }

    public EncryptedRecordType RecordType { get; set; }

    public Guid OwnerUserId { get; set; }

    public Guid? ParentResourceId { get; set; }

    public long Revision { get; set; }

    public byte[] Payload { get; set; } = [];

    public bool IsDeleted { get; set; }

    public long SequenceNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void MarkAsDeleted() => IsDeleted = true;

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}
