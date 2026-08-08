using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class SharedResource
{
    public Guid Id { get; set; }

    public SharedResourceType Type { get; set; }

    public Guid OwnerUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
