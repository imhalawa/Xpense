namespace Xpense.Domain.Enums;

public static class CurrencyParser
{
    public static bool TryParse(string value, out Currency currency)
    {
        currency = default;

        return !string.IsNullOrWhiteSpace(value)
               && Enum.GetNames<Currency>().Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
               && Enum.TryParse(value, ignoreCase: true, out currency);
    }
}
