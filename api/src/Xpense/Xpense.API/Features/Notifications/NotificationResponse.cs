using System.Text.Json;
using System.Text.Json.Nodes;
using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Notifications;

/// <summary>
/// Shared by every notification slice: a notification looks the same however you fetched it.
/// <para>
/// <c>Payload</c> is JSON rather than a string. It is stored as jsonb and emitted inline, so a client
/// reads an object -- serializing it as a quoted string would make every consumer parse it twice.
/// </para>
/// </summary>
public sealed record NotificationResponse(
    int Id,
    string Kind,
    string Title,
    string Message,
    JsonNode? Payload,
    string? ReadAt,
    string CreatedAt)
{
    public static NotificationResponse Of(Notification notification) => new(
        notification.Id,
        notification.Kind.ToString(),
        notification.Title,
        notification.Message,
        Parse(notification.Payload),
        Timestamps.Iso(notification.ReadAt),
        Timestamps.Iso(notification.CreatedAt));

    private static JsonNode? Parse(string payload) => JsonNode.Parse(payload);
}

/// <summary>
/// A page of notifications. The same envelope shape as TransactionPageResponse, because paging should
/// not look different depending on which collection you asked for.
/// </summary>
public sealed record NotificationPageResponse(
    NotificationResponse[] Notifications,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    int UnreadItems);

/// <summary>Just the count, for a badge that should not have to fetch a page to draw itself.</summary>
public sealed record UnreadCountResponse(int Unread);
