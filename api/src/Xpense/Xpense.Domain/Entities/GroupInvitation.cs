using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class GroupInvitation
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid InvitedByUserId { get; set; }

    public string? TargetNormalizedEmail { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public InvitationState State { get; set; }

    public DateTime ExpiresAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }

    public byte[]? GroupKeyEnvelope { get; set; }

    public int? EnvelopeProtocolVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void Revoke(DateTime now)
    {
        State = InvitationState.Revoked;
        UpdatedAt = now;
    }
}
