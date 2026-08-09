namespace Xpense.Domain.Entities;

public class PendingPasskeyAssertion
{
    public Guid Id { get; set; }

    public string? NormalizedEmail { get; set; }

    public string AssertionState { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
