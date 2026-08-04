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

        public static Money operator +(Money lhs, Money rhs)
        {
            if (lhs.Currency != rhs.Currency)
                throw new IncompatibleCurrencyOperationException();

            return OfCents(lhs.Cents + rhs.Cents, lhs.Currency);
        }

        public static Money operator -(Money lhs, Money rhs)
        {
            if (lhs.Currency != rhs.Currency)
                throw new IncompatibleCurrencyOperationException();

            return OfCents(lhs.Cents - rhs.Cents, lhs.Currency);
        }

        // ponytail: no Money*Money or Money/Money. Multiplying two amounts yields money-squared
        // and dividing yields a dimensionless ratio -- neither is a Money. Add Money*decimal
        // scaling if a caller ever actually needs it.
    }
}
