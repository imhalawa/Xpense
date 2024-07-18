using Xpense.Services.Enums;
using Xpense.Services.Exceptions;

namespace Xpense.Services.ValueObjects
{
    public class Money(long value, Currency currency)
    {

        public long Cents { get; set; } = value;
        public Currency Currency { get; set; } = currency;

        public static Money Zero => OfCents(0);
        public static Money OfCents(long cents, Currency currency = Currency.EUR)
        {
            return new Money(cents, currency);
        }

        public decimal ToSingle()
        {
            return this.Cents / 100;
        }

        public static Money operator +(Money lhs, Money rhs)
        {
            if (lhs.Currency == rhs.Currency)
            {
                return OfCents(lhs.Cents + rhs.Cents);
            }
            throw new IncompaitableCurrencyOperationException();
        }

        public static Money operator -(Money lhs, Money rhs)
        {
            if (lhs.Currency == rhs.Currency)
            {
                return OfCents(lhs.Cents - rhs.Cents);
            }
            throw new IncompaitableCurrencyOperationException();
        }

        public static Money operator /(Money lhs, Money rhs)
        {
            if (lhs.Currency == rhs.Currency)
            {
                return OfCents(lhs.Cents / rhs.Cents);
            }
            throw new IncompaitableCurrencyOperationException();
        }

        public static Money operator *(Money lhs, Money rhs)
        {
            if (lhs.Currency == rhs.Currency)
            {
                return OfCents(lhs.Cents * rhs.Cents);
            }
            throw new IncompaitableCurrencyOperationException();
        }
    }
}
