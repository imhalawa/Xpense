using FluentAssertions;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.ValueObjects;
using Entities = Xpense.Domain.Entities;

namespace Xpense.Tests.Unit;

[TestFixture]
public class TransactionTests
{
    private static readonly DateTime OccurredAt = new(2026, 7, 26, 9, 30, 0, DateTimeKind.Utc);

    [Test]
    public void Transfer_withdraws_from_the_source_and_deposits_into_the_destination()
    {
        var source = Account("1000000000", 2000, currency: Currency.USD);
        var destination = Account("2000000000", 300, currency: Currency.USD);

        var transaction = Entities.Transaction.Transfer(
            source, destination, Money.OfMinorUnits(1234, Currency.USD), "Shared rent", null, OccurredAt);

        source.BalanceMinorUnits.Should().Be(766);
        destination.BalanceMinorUnits.Should().Be(1534);
        transaction.AmountMinorUnits.Should().Be(1234);
        transaction.Amount.Should().BeEquivalentTo(Money.OfMinorUnits(1234, Currency.USD));
        transaction.Currency.Should().Be(Currency.USD);
        transaction.Reason.Should().Be("Shared rent");
        transaction.OccurredAt.Should().Be(OccurredAt);
        transaction.Kind.Should().Be(TransactionKind.Transfer);
    }

    [Test]
    public void Transfer_carries_neither_a_category_nor_a_merchant()
    {
        var transaction = Entities.Transaction.Transfer(
            Account("1000000000", 2000), Account("2000000000", 300), Money.OfMinorUnits(100), null, null, OccurredAt);

        transaction.Category.Should().BeNull();
        transaction.CategoryId.Should().BeNull();
        transaction.Merchant.Should().BeNull();
        transaction.MerchantId.Should().BeNull();
    }

    [Test]
    public void Transfer_rejects_identical_accounts_without_moving_money()
    {
        var account = Account("1000000000", 2000);
        account.Id = 7;

        var act = () => Entities.Transaction.Transfer(account, account, Money.OfMinorUnits(100), null, null, OccurredAt);

        act.Should().Throw<InvalidTransactionException>();
        account.BalanceMinorUnits.Should().Be(2000);
    }

    [Test]
    public void Transfer_rejects_an_amount_beyond_the_source_balance_without_moving_money()
    {
        var source = Account("1000000000", 500);
        var destination = Account("2000000000", 300);

        var act = () => Entities.Transaction.Transfer(source, destination, Money.OfMinorUnits(501), null, null, OccurredAt);

        act.Should().Throw<InsufficientFundsForTransferException>();
        source.BalanceMinorUnits.Should().Be(500);
        destination.BalanceMinorUnits.Should().Be(300);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Transfer_rejects_a_non_positive_amount(long minorUnits)
    {
        var source = Account("1000000000", 2000);
        var destination = Account("2000000000", 300);

        var act = () => Entities.Transaction.Transfer(
            source, destination, Money.OfMinorUnits(minorUnits), null, null, OccurredAt);

        act.Should().Throw<InvalidTransactionException>();
        source.BalanceMinorUnits.Should().Be(2000);
    }

    [Test]
    public void Transfer_treats_a_blank_reason_as_absent()
    {
        var transaction = Entities.Transaction.Transfer(
            Account("1000000000", 2000), Account("2000000000", 300), Money.OfMinorUnits(100), "   ", null, OccurredAt);

        transaction.Reason.Should().BeNull();
    }

    [Test]
    public void Transfer_refuses_accounts_in_different_currencies()
    {
        var source = Account("1000000000", 2000, currency: Currency.EUR);
        var destination = Account("2000000000", 300, currency: Currency.USD);

        var act = () => Entities.Transaction.Transfer(
            source, destination, Money.OfMinorUnits(100), null, null, OccurredAt);

        act.Should().Throw<InvalidTransactionException>()
            .WithMessage("*different currencies*");
        source.BalanceMinorUnits.Should().Be(2000);
        destination.BalanceMinorUnits.Should().Be(300);
    }

    [Test]
    public void Transfer_refuses_an_amount_in_a_currency_the_accounts_do_not_hold()
    {
        var source = Account("1000000000", 2000, currency: Currency.EUR);
        var destination = Account("2000000000", 300, currency: Currency.EUR);

        var act = () => Entities.Transaction.Transfer(
            source, destination, Money.OfMinorUnits(100, Currency.USD), null, null, OccurredAt);

        act.Should().Throw<CurrencyMismatchException>();
        source.BalanceMinorUnits.Should().Be(2000);
    }

    [Test]
    public void Income_deposits_into_the_destination_and_names_no_source()
    {
        var destination = Account("1000000000", 1000);

        var transaction = Entities.Transaction.Income(
            destination, Money.OfMinorUnits(250), Category(), Merchant(), null, OccurredAt);

        destination.BalanceMinorUnits.Should().Be(1250);
        transaction.Kind.Should().Be(TransactionKind.Income);
        transaction.DestinationAccount.Should().BeSameAs(destination);
        transaction.SourceAccount.Should().BeNull();
        transaction.Category.Should().NotBeNull();
        transaction.Merchant.Should().NotBeNull();
    }

    [Test]
    public void Expense_withdraws_from_the_source_and_names_no_destination()
    {
        var source = Account("1000000000", 1000);

        var transaction = Entities.Transaction.Expense(
            source, Money.OfMinorUnits(250), Category(), Merchant(), null, OccurredAt);

        source.BalanceMinorUnits.Should().Be(750);
        transaction.Kind.Should().Be(TransactionKind.Expense);
        transaction.SourceAccount.Should().BeSameAs(source);
        transaction.DestinationAccount.Should().BeNull();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Income_and_expense_reject_a_non_positive_amount(long minorUnits)
    {
        var account = Account("1000000000", 1000);

        var income = () => Entities.Transaction.Income(
            account, Money.OfMinorUnits(minorUnits), Category(), Merchant(), null, OccurredAt);
        var expense = () => Entities.Transaction.Expense(
            account, Money.OfMinorUnits(minorUnits), Category(), Merchant(), null, OccurredAt);

        income.Should().Throw<InvalidTransactionException>();
        expense.Should().Throw<InvalidTransactionException>();
        account.BalanceMinorUnits.Should().Be(1000);
    }

    [Test]
    public void Income_refuses_an_amount_in_a_currency_the_account_does_not_hold()
    {
        var destination = Account("1000000000", 1000, currency: Currency.EUR);

        var act = () => Entities.Transaction.Income(
            destination, Money.OfMinorUnits(100, Currency.USD), Category(), Merchant(), null, OccurredAt);

        act.Should().Throw<CurrencyMismatchException>();
        destination.BalanceMinorUnits.Should().Be(1000);
    }

    [Test]
    public void Kind_is_correct_before_the_transaction_is_saved()
    {
        var source = Account("1000000000", 2000, id: 0);
        var destination = Account("2000000000", 300, id: 0);

        var transfer = Entities.Transaction.Transfer(
            source, destination, Money.OfMinorUnits(100), null, null, OccurredAt);
        var expense = Entities.Transaction.Expense(
            Account("3000000000", 2000, id: 0), Money.OfMinorUnits(100), Category(), Merchant(), null, OccurredAt);

        transfer.SourceAccountId.Should().BeNull("EF has not assigned keys yet");
        transfer.Kind.Should().Be(TransactionKind.Transfer);
        expense.Kind.Should().Be(TransactionKind.Expense);
    }

    [Test]
    public void Comparing_money_in_different_currencies_throws_rather_than_guessing()
    {
        var act = () => Money.OfMinorUnits(100, Currency.EUR) < Money.OfMinorUnits(100, Currency.USD);

        act.Should().Throw<IncompatibleCurrencyOperationException>();
    }

    private static Entities.Account Account(
        string number,
        long balanceMinorUnits,
        int id = 0,
        Currency currency = Currency.EUR) => new()
    {
        Id = id,
        AccountNumber = number,
        Label = number,
        BalanceMinorUnits = balanceMinorUnits,
        Currency = currency,
        CreatedAt = DateTime.UtcNow
    };

    private static Entities.Category Category() => new()
    {
        Label = "Groceries",
        Priority = new Entities.Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow },
        CreatedAt = DateTime.UtcNow
    };

    private static Entities.Merchant Merchant() => new()
    {
        Label = "Albert Heijn",
        CreatedAt = DateTime.UtcNow
    };
}
