namespace Xpense.Domain.Events;

public interface IEventBus
{
    Task Emit<TBody>(Event<TBody> @event, CancellationToken cancellationToken = default)
        where TBody : EventBody;
}
