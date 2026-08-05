namespace Xpense.Domain.Enums;

public static class RecurrenceParser
{
    /// <summary>
    /// Parses a recurrence name, case-insensitively, rejecting numeric input.
    /// <para>
    /// Same reason as <see cref="CurrencyParser"/>: <c>Enum.TryParse</c> accepts "0" and every other
    /// number in range, so a client posting <c>"recurrence": "0"</c> would silently get
    /// <see cref="Recurrence.None"/> -- and a budget that does not repeat when it was meant to.
    /// </para>
    /// </summary>
    public static bool TryParse(string value, out Recurrence recurrence)
    {
        recurrence = default;

        return !string.IsNullOrWhiteSpace(value)
               && Enum.GetNames<Recurrence>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(value, ignoreCase: true, out recurrence);
    }
}
