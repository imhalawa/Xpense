using Xpense.Domain.Events;

namespace Xpense.Notifications.Rules;

public interface INotificationRule<TBody> where TBody : EventBody
{
    Task<IReadOnlyList<NotificationDraft>> Evaluate(Event<TBody> @event, CancellationToken cancellationToken);
}
