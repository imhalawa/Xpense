namespace Xpense.Domain.Enums;

public static class CurrencyParser
{
    /// <summary>
    /// Parses a currency name, case-insensitively, rejecting numeric input.
    /// <para>
    /// Enum.TryParse happily accepts "0" and any other number in range, which would let a
    /// client post <c>"currency": "0"</c> and silently get EUR. Checking the value against the
    /// defined names first is what stops that.
    /// </para>
    /// </summary>
    public static bool TryParse(string value, out Currency currency)
    {
        currency = default;

        return !string.IsNullOrWhiteSpace(value)
               && Enum.GetNames<Currency>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(value, ignoreCase: true, out currency);
    }
}
