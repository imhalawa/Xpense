using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;

namespace Xpense.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public long AmountMinorUnits { get; set; }

        public Currency Currency { get; set; }

        public Money Amount => Money.OfMinorUnits(AmountMinorUnits, Currency);

        public DateTime OccurredAt { get; set; }

        public string? Reason { get; set; }

        public int? SourceAccountId { get; set; }

        public Account? SourceAccount { get; set; }

        public int? DestinationAccountId { get; set; }

        public Account? DestinationAccount { get; set; }

        public int? CategoryId { get; set; }

        public Category? Category { get; set; }

        public int? MerchantId { get; set; }

        public Merchant? Merchant { get; set; }

        public virtual ICollection<Tag>? Tags { get; set; }

        public TransactionKind Kind =>
            !HasSource ? TransactionKind.Income
            : !HasDestination ? TransactionKind.Expense
            : TransactionKind.Transfer;

        private bool HasSource => SourceAccountId is not null || SourceAccount is not null;

        private bool HasDestination => DestinationAccountId is not null || DestinationAccount is not null;

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

        public void ReverseBalanceEffect()
        {
            switch (Kind)
            {
                case TransactionKind.Income:
                    DestinationAccount!.Withdraw(Amount);
                    break;
                case TransactionKind.Expense:
                    SourceAccount!.Deposit(Amount);
                    break;
                case TransactionKind.Transfer:
                    SourceAccount!.Deposit(Amount);
                    DestinationAccount!.Withdraw(Amount);
                    break;
                default:
                    throw new InvalidTransactionException("The transaction kind cannot be reversed.");
            }
        }

        public void ReplaceWith(Transaction replacement)
        {
            AmountMinorUnits = replacement.AmountMinorUnits;
            Currency = replacement.Currency;
            OccurredAt = replacement.OccurredAt;
            Reason = replacement.Reason;
            SourceAccount = replacement.SourceAccount;
            SourceAccountId = replacement.SourceAccount?.Id is > 0
                ? replacement.SourceAccount.Id
                : replacement.SourceAccountId;
            DestinationAccount = replacement.DestinationAccount;
            DestinationAccountId = replacement.DestinationAccount?.Id is > 0
                ? replacement.DestinationAccount.Id
                : replacement.DestinationAccountId;
            Category = replacement.Category;
            CategoryId = replacement.Category?.Id is > 0
                ? replacement.Category.Id
                : replacement.CategoryId;
            Merchant = replacement.Merchant;
            MerchantId = replacement.Merchant?.Id is > 0
                ? replacement.Merchant.Id
                : replacement.MerchantId;
            Tags = replacement.Tags;
            Touch();
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
