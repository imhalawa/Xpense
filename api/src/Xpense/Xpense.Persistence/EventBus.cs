using System.Text.Json;
using Xpense.Domain.Entities;
using Xpense.Domain.Events;

namespace Xpense.Persistence;

public sealed class EventBus(XpenseDbContext dbContext) : IEventBus
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task Emit<TBody>(Event<TBody> @event, CancellationToken cancellationToken = default)
        where TBody : EventBody
    {
        var body = JsonSerializer.Serialize(@event.Body, Json);

        dbContext.Events.Add(EventRecord.Of(@event, body));

        return Task.CompletedTask;
    }
}
