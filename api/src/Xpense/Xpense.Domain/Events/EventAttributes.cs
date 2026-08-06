namespace Xpense.Domain.Events;

public sealed record EventAttributes(
    Guid EventId,
    string Type,
    DateTime OccurredAt,
    string Source,
    int Version);
