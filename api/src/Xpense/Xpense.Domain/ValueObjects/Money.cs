using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;

namespace Xpense.Domain.ValueObjects
{
    public class Money(long minorUnits, Currency currency)
    {
        /// <summary>
        /// The amount as a whole number of the currency's smallest indivisible unit. Deliberately
        /// not "cents": that holds for EUR and USD and is wrong for the first currency without them.
        /// </summary>
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

        /// <summary>
        /// Comparing amounts in different currencies is meaningless, so it throws rather than
        /// quietly returning an answer based on the raw numbers.
        /// </summary>
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

        // ponytail: no Money*Money or Money/Money. Multiplying two amounts yields money-squared
        // and dividing yields a dimensionless ratio -- neither is a Money. Add Money*decimal
        // scaling if a caller ever actually needs it.
    }
}
