using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Entities
{
    /// <summary>
    /// A single recorded movement of money. One entity records all three kinds: each side is
    /// either an account inside Xpense or nothing, and nothing means the money crossed the system
    /// boundary -- in which case <see cref="Merchant"/> names who was on that side.
    /// <para>
    /// This replaced a <c>Transaction</c> naming one account plus a <c>Transfer</c> with two
    /// <c>TransferLeg</c> rows. The legs held nothing their parent did not already hold. See
    /// docs/adr/0001-one-transaction-entity-with-two-nullable-sides.md.
    /// </para>
    /// </summary>
    public class Transaction : BaseEntity
    {
        /// <summary>Amount in minor units of <see cref="Currency"/>. Mapped; prefer <see cref="Amount"/>.</summary>
        public long AmountMinorUnits { get; set; }

        public Currency Currency { get; set; }

        /// <summary>The amount as money. Not mapped -- projected from the two columns above.</summary>
        public Money Amount => Money.OfMinorUnits(AmountMinorUnits, Currency);

        /// <summary>When the money actually moved, as told to Xpense. Distinct from <see cref="BaseEntity.CreatedAt"/>.</summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>Free text explaining why this happened.</summary>
        public string? Reason { get; set; }

        /// <summary>The account money left. Null means it came from outside Xpense.</summary>
        public int? SourceAccountId { get; set; }

        public Account? SourceAccount { get; set; }

        /// <summary>The account money arrived in. Null means it went outside Xpense.</summary>
        public int? DestinationAccountId { get; set; }

        public Account? DestinationAccount { get; set; }

        /// <summary>Required when one side is outside Xpense; null on a transfer.</summary>
        public int? CategoryId { get; set; }

        public Category? Category { get; set; }

        /// <summary>The party on the outside side. Required when one side is outside Xpense; null on a transfer.</summary>
        public int? MerchantId { get; set; }

        public Merchant? Merchant { get; set; }

        public virtual ICollection<Tag>? Tags { get; set; }

        /// <summary>
        /// Derived, never stored, so a row cannot contradict itself.
        /// <para>
        /// Both the foreign key and the navigation are checked because a transaction built by one
        /// of the factories below carries navigations but no keys until it is saved.
        /// </para>
        /// </summary>
        public TransactionKind Kind =>
            !HasSource ? TransactionKind.Income
            : !HasDestination ? TransactionKind.Expense
            : TransactionKind.Transfer;

        private bool HasSource => SourceAccountId is not null || SourceAccount is not null;

        private bool HasDestination => DestinationAccountId is not null || DestinationAccount is not null;

        /// <summary>Money arriving from outside Xpense.</summary>
        public static Transaction Income(
            Account destination,
            Money amount,
            Category category,
            Merchant merchant,
            IEnumerable<Tag>? tags,
            DateTime occurredAt)
        {
            RequirePositive(amount);

            destination.Deposit(amount);

            var transaction = Build(amount, occurredAt, tags);
            transaction.DestinationAccount = destination;
            transaction.Category = category;
            transaction.Merchant = merchant;
            return transaction;
        }

        /// <summary>Money leaving for outside Xpense.</summary>
        public static Transaction Expense(
            Account source,
            Money amount,
            Category category,
            Merchant merchant,
            IEnumerable<Tag>? tags,
            DateTime occurredAt)
        {
            RequirePositive(amount);

            source.Withdraw(amount);

            var transaction = Build(amount, occurredAt, tags);
            transaction.SourceAccount = source;
            transaction.Category = category;
            transaction.Merchant = merchant;
            return transaction;
        }

        /// <summary>
        /// Money moving between two accounts inside Xpense. Carries neither category nor merchant:
        /// there is no shop and no spending class, because the money is still yours.
        /// </summary>
        public static Transaction Transfer(
            Account source,
            Account destination,
            Money amount,
            string? reason,
            IEnumerable<Tag>? tags,
            DateTime occurredAt)
        {
            RequirePositive(amount);

            if (ReferenceEquals(source, destination) || (source.Id != 0 && source.Id == destination.Id))
                throw new InvalidTransactionException("Source and destination accounts must be different.");

            // Xpense holds multiple currencies but does not convert between them. Both accounts and
            // the amount have to agree; otherwise this moves the wrong quantity of money, which is
            // exactly what it used to do when a balance was a currency-less decimal.
            if (source.Currency != destination.Currency)
                throw new InvalidTransactionException(
                    "Cannot transfer between accounts in different currencies: "
                    + $"{source.AccountNumber} is {source.Currency}, {destination.AccountNumber} is {destination.Currency}.");

            if (amount.Currency != source.Currency)
                throw new CurrencyMismatchException(source.AccountNumber, source.Currency, amount.Currency);

            if (source.Balance < amount)
                throw new InsufficientFundsForTransferException(source.Id, source.Balance, amount);

            source.Withdraw(amount);
            destination.Deposit(amount);

            var transaction = Build(amount, occurredAt, tags);
            transaction.SourceAccount = source;
            transaction.DestinationAccount = destination;
            transaction.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            return transaction;
        }

        private static Transaction Build(Money amount, DateTime occurredAt, IEnumerable<Tag>? tags) =>
            new()
            {
                AmountMinorUnits = amount.MinorUnits,
                Currency = amount.Currency,
                OccurredAt = occurredAt,
                CreatedAt = DateTime.UtcNow,
                Tags = tags?.ToList()
            };

        private static void RequirePositive(Money amount)
        {
            if (amount.MinorUnits <= 0)
                throw new InvalidTransactionException("Transaction amount must be positive.");
        }
    }
}
