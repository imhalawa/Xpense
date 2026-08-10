namespace Xpense.Domain.Entities;

public class SyncOperation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid EncryptedRecordId { get; set; }

    public DateTime CreatedAt { get; set; }
}
