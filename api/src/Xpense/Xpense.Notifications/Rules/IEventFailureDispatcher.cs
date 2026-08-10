using System.Text.Json;
using Xpense.Domain.Entities;
using Xpense.Domain.Events;

namespace Xpense.Notifications.Rules;

public interface IEventFailureHandler<TBody> where TBody : EventBody
{
    Task Handle(Event<TBody> @event, Exception exception, int attempt, CancellationToken cancellationToken);
}

public interface IEventFailureDispatcher
{
    string EventType { get; }

    Task Dispatch(EventRecord record, Exception exception, CancellationToken cancellationToken);
}

public sealed class EventFailureDispatcher<TBody>(IEnumerable<IEventFailureHandler<TBody>> handlers)
    : IEventFailureDispatcher where TBody : EventBody
{
    public string EventType => typeof(TBody).Name;

    public async Task Dispatch(EventRecord record, Exception exception, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Deserialize<TBody>(record.Body, EventJson.Options)
                   ?? throw new InvalidOperationException($"Event {record.EventId} could not be read.");
        var @event = new Event<TBody>(
            new EventAttributes(record.EventId, record.Type, record.OccurredAt, record.Source, record.Version),
            body);

        foreach (var handler in handlers)
            await handler.Handle(@event, exception, record.Attempts + 1, cancellationToken);
    }
}
