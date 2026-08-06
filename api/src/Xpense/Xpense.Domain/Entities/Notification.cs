using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class Notification : BaseEntity
{
    public int? OwnerId { get; set; }

    public Guid EventId { get; set; }

    public NotificationKind Kind { get; set; }

    public required string Title { get; set; }

    public required string Message { get; set; }

    public required string Payload { get; set; }

    public required string PayloadHash { get; set; }

    public DateTime? ReadAt { get; set; }

    public void MarkAsRead()
    {
        if (ReadAt is not null)
            return;

        ReadAt = ToStorablePrecision(DateTime.UtcNow);
        Touch();
    }

    private static DateTime ToStorablePrecision(DateTime value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new DateTime(value.Ticks - value.Ticks % ticksPerMicrosecond, value.Kind);
    }
}
