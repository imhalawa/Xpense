using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class ResourceGrant
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public SharedResourceType ResourceType { get; set; }

    public Guid ResourceId { get; set; }

    public GrantPermission Permission { get; set; }

    public GrantState State { get; set; }

    public Guid GrantedByUserId { get; set; }

    public Guid? KeyEnvelopeReference { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
