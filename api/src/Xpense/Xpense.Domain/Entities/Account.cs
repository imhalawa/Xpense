using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Entities
{
    public class Account : BaseEntity
    {
        public required string Label { get; set; }

        public required string AccountNumber { get; set; }

        public long BalanceMinorUnits { get; set; }

        public Currency Currency { get; set; }

        public Money Balance => Money.OfMinorUnits(BalanceMinorUnits, Currency);

        public bool IsDefault { get; set; }

        public void Deposit(Money amount)
        {
            RequireMatchingCurrency(amount);
            BalanceMinorUnits += amount.MinorUnits;
            Touch();
        }

        public void Withdraw(Money amount)
        {
            RequireMatchingCurrency(amount);
            BalanceMinorUnits -= amount.MinorUnits;
            Touch();
        }

        private void RequireMatchingCurrency(Money amount)
        {
            if (amount.Currency != Currency)
                throw new CurrencyMismatchException(AccountNumber, Currency, amount.Currency);
        }
    }
}
