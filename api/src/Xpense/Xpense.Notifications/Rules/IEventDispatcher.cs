using System.Text.Json;
using Xpense.Domain.Entities;
using Xpense.Domain.Events;

namespace Xpense.Notifications.Rules;

public interface IEventDispatcher
{
    string EventType { get; }

    Task<IReadOnlyList<NotificationDraft>> Dispatch(EventRecord record, CancellationToken cancellationToken);
}

public sealed class EventDispatcher<TBody>(IEnumerable<INotificationRule<TBody>> rules) : IEventDispatcher
    where TBody : EventBody
{
    public string EventType => typeof(TBody).Name;

    public async Task<IReadOnlyList<NotificationDraft>> Dispatch(EventRecord record, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Deserialize<TBody>(record.Body, EventJson.Options)
                   ?? throw new InvalidOperationException(
                       $"Event {record.EventId} of type {record.Type} deserialized to null.");

        var @event = new Event<TBody>(
            new EventAttributes(record.EventId, record.Type, record.OccurredAt, record.Source, record.Version),
            body);

        var drafts = new List<NotificationDraft>();

        foreach (var rule in rules)
            drafts.AddRange(await rule.Evaluate(@event, cancellationToken));

        return drafts;
    }
}
