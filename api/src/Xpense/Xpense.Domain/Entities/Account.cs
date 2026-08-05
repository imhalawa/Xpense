using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Entities
{
    /// <summary>
    /// An account holds money in exactly one currency.
    /// <para>
    /// The balance is stored in minor units rather than as a decimal so it shares a
    /// representation with <see cref="ValueObjects.Money"/>. Previously it was a bare decimal
    /// with no currency at all, which let a USD transfer move money out of a EUR account
    /// without complaint.
    /// </para>
    /// <para>
    /// There is no collection of transactions here. A <see cref="Transaction"/> points at up to
    /// two accounts, so one navigation cannot express the relationship and two would only ever be
    /// queried separately anyway.
    /// </para>
    /// </summary>
    public class Account : BaseEntity
    {
        /// <summary>The human-readable name of the account.</summary>
        public required string Label { get; set; }

        /// <summary>The public identifier of the account. <see cref="BaseEntity.Id"/> stays internal.</summary>
        public required string AccountNumber { get; set; }

        /// <summary>Balance in minor units of <see cref="Currency"/>. Mapped; prefer <see cref="Balance"/>.</summary>
        public long BalanceMinorUnits { get; set; }

        /// <summary>The currency this account is denominated in.</summary>
        public Currency Currency { get; set; }

        /// <summary>The balance as money. Not mapped -- projected from the two columns above.</summary>
        public Money Balance => Money.OfMinorUnits(BalanceMinorUnits, Currency);

        /// <summary>Whether this account is the default one for transactions.</summary>
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

        /// <summary>
        /// There is no conversion here: an amount in another currency cannot be applied to this
        /// account. Adding FX would mean converting before this point, never inside it.
        /// </summary>
        private void RequireMatchingCurrency(Money amount)
        {
            if (amount.Currency != Currency)
                throw new CurrencyMismatchException(AccountNumber, Currency, amount.Currency);
        }
    }
}
