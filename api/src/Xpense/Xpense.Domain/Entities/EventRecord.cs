using Xpense.Domain.Events;

namespace Xpense.Domain.Entities;

public class EventRecord : BaseEntity
{
    public const int MaxAttempts = 5;

    public Guid EventId { get; set; }

    public required string Type { get; set; }

    public DateTime OccurredAt { get; set; }

    public required string Source { get; set; }

    public int Version { get; set; }

    public required string Body { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public static EventRecord Of<TBody>(Event<TBody> @event, string body) where TBody : EventBody =>
        new()
        {
            EventId = @event.Attributes.EventId,
            Type = @event.Attributes.Type,
            OccurredAt = @event.Attributes.OccurredAt,
            Source = @event.Attributes.Source,
            Version = @event.Attributes.Version,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

    public void Succeeded()
    {
        ProcessedAt = DateTime.UtcNow;
        LastError = null;
        Touch();
    }

    public void Failed(string error)
    {
        Attempts++;
        LastError = error;

        if (Attempts >= MaxAttempts)
            ProcessedAt = DateTime.UtcNow;

        Touch();
    }
}
