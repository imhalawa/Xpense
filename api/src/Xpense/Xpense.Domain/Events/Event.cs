namespace Xpense.Domain.Events;

public sealed record Event<TBody>(EventAttributes Attributes, TBody Body)
    where TBody : EventBody;

public static class Event
{
    public const string DefaultSource = "Xpense.API";

    public const int CurrentVersion = 1;

    public static Event<TBody> Of<TBody>(
        TBody body,
        DateTime? occurredAt = null,
        string source = DefaultSource)
        where TBody : EventBody =>
        new(
            new EventAttributes(
                Guid.CreateVersion7(),
                typeof(TBody).Name,
                occurredAt ?? DateTime.UtcNow,
                source,
                CurrentVersion),
            body);
}
