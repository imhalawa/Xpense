using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class GroupMembership
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public MembershipRole Role { get; set; }

    public MembershipState State { get; set; }

    public byte[]? GroupKeyEnvelope { get; set; }

    public int EnvelopeProtocolVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool Revoke(DateTime now)
    {
        State = MembershipState.Revoked;
        GroupKeyEnvelope = null;
        RevokedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void ChangeRole(MembershipRole role, DateTime now)
    {
        Role = role;
        UpdatedAt = now;
    }

    public void AwaitOwnerApproval(DateTime now)
    {
        Role = MembershipRole.Member;
        State = MembershipState.AwaitingOwnerApproval;
        GroupKeyEnvelope = null;
        EnvelopeProtocolVersion = 0;
        RevokedAt = null;
        UpdatedAt = now;
    }

    public void Activate(byte[] groupKeyEnvelope, int envelopeProtocolVersion, DateTime now)
    {
        Role = MembershipRole.Member;
        State = MembershipState.Active;
        GroupKeyEnvelope = groupKeyEnvelope;
        EnvelopeProtocolVersion = envelopeProtocolVersion;
        RevokedAt = null;
        UpdatedAt = now;
    }
}
