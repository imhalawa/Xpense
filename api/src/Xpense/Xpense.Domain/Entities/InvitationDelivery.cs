using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class InvitationDelivery
{
    public Guid Id { get; set; }

    public Guid InvitationId { get; set; }

    public string EmailAddress { get; set; } = string.Empty;

    public string? ProtectedPayload { get; set; }

    public DeliveryStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
