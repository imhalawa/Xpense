using System.Text.Json;

namespace Xpense.Notifications.Rules;

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
