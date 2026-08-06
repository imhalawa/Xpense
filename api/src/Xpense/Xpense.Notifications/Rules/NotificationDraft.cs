using Xpense.Domain.Enums;

namespace Xpense.Notifications.Rules;

public sealed record NotificationDraft(
    NotificationKind Kind,
    string Title,
    string Message,
    object Payload);
