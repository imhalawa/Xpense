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
}
