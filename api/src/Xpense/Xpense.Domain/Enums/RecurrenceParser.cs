namespace Xpense.Domain.Enums;

public static class RecurrenceParser
{
    public static bool TryParse(string value, out Recurrence recurrence)
    {
        recurrence = default;

        return !string.IsNullOrWhiteSpace(value)
               && Enum.GetNames<Recurrence>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(value, ignoreCase: true, out recurrence);
    }
}
