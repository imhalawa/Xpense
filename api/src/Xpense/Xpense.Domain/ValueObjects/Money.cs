using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;

namespace Xpense.Domain.ValueObjects
{
    public class Money(long minorUnits, Currency currency)
    {
        public long MinorUnits { get; } = minorUnits;

        public Currency Currency { get; } = currency;

        public static Money Zero => OfMinorUnits(0);

        public static Money OfMinorUnits(long minorUnits, Currency currency = Currency.EUR)
        {
            return new Money(minorUnits, currency);
        }

        public decimal ToDecimal()
        {
            return MinorUnits / 100m;
        }

        public override string ToString() => $"{ToDecimal():0.00} {Currency}";

        public static Money operator +(Money lhs, Money rhs)
        {
            RequireSameCurrency(lhs, rhs);
            return OfMinorUnits(lhs.MinorUnits + rhs.MinorUnits, lhs.Currency);
        }

        public static Money operator -(Money lhs, Money rhs)
        {
            RequireSameCurrency(lhs, rhs);
            return OfMinorUnits(lhs.MinorUnits - rhs.MinorUnits, lhs.Currency);
        }

        public static bool operator <(Money lhs, Money rhs) => Compare(lhs, rhs) < 0;

        public static bool operator >(Money lhs, Money rhs) => Compare(lhs, rhs) > 0;

        public static bool operator <=(Money lhs, Money rhs) => Compare(lhs, rhs) <= 0;

        public static bool operator >=(Money lhs, Money rhs) => Compare(lhs, rhs) >= 0;

        private static int Compare(Money lhs, Money rhs)
        {
            RequireSameCurrency(lhs, rhs);
            return lhs.MinorUnits.CompareTo(rhs.MinorUnits);
        }

        private static void RequireSameCurrency(Money lhs, Money rhs)
        {
            if (lhs.Currency != rhs.Currency)
                throw new IncompatibleCurrencyOperationException();
        }

    }
}
