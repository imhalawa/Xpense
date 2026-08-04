using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;

namespace Xpense.Domain.ValueObjects
{
    public class Money(long value, Currency currency)
    {
        public long Cents { get; } = value;
        public Currency Currency { get; } = currency;

        public static Money Zero => OfCents(0);

        public static Money OfCents(long cents, Currency currency = Currency.EUR)
        {
            return new Money(cents, currency);
        }

        public decimal ToDecimal()
        {
            return Cents / 100m;
        }

        public override string ToString() => $"{ToDecimal():0.00} {Currency}";

        public static Money operator +(Money lhs, Money rhs)
        {
            RequireSameCurrency(lhs, rhs);
            return OfCents(lhs.Cents + rhs.Cents, lhs.Currency);
        }

        public static Money operator -(Money lhs, Money rhs)
        {
            RequireSameCurrency(lhs, rhs);
            return OfCents(lhs.Cents - rhs.Cents, lhs.Currency);
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
            return lhs.Cents.CompareTo(rhs.Cents);
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
