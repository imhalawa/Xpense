using FluentAssertions;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Domain.Transfers;
using Xpense.Domain.ValueObjects;

namespace Xpense.Tests.Unit;

/// <summary>
/// Replaces TransferTransactionUseCaseTests. Those spun up a real SQLite database to assert
/// pure invariants; MoneyTransfer has no persistence dependency, so these are actual unit
/// tests. Rollback behaviour is covered by the integration tests that assert no balance
/// changes and no transfer rows survive a rejected transfer.
/// </summary>
[TestFixture]
public class MoneyTransferTests
{
    private static readonly DateTime OccurredAt = new(2026, 7, 26, 9, 30, 0, DateTimeKind.Utc);

    [Test]
    public void Between_debits_the_source_credits_the_destination_and_writes_two_legs()
    {
        var source = Account("1000000000", 2000, currency: Currency.USD);
        var destination = Account("2000000000", 300, currency: Currency.USD);

        var transfer = MoneyTransfer.Between(
            source, destination, Money.OfCents(1234, Currency.USD), "Shared rent", OccurredAt);

        source.BalanceCents.Should().Be(766);
        destination.BalanceCents.Should().Be(1534);
        transfer.Amount.Should().Be(1234);
        transfer.Currency.Should().Be(Currency.USD);
        transfer.Reason.Should().Be("Shared rent");
        transfer.CreatedOn.Should().Be(OccurredAt);
        transfer.Legs.Select(leg => leg.Direction)
            .Should().BeEquivalentTo([TransferLegDirection.Debit, TransferLegDirection.Credit]);
        transfer.Legs.Should().OnlyContain(leg => leg.Amount == 1234 && leg.Currency == Currency.USD);
    }

    [Test]
    public void Between_rejects_identical_accounts_without_moving_money()
    {
        var account = Account("1000000000", 2000);
        account.Id = 7;

        var act = () => MoneyTransfer.Between(account, account, Money.OfCents(100), null, OccurredAt);

        act.Should().Throw<InvalidTransferException>();
        account.BalanceCents.Should().Be(2000);
    }

    [Test]
    public void Between_rejects_an_amount_beyond_the_source_balance_without_moving_money()
    {
        var source = Account("1000000000", 500);
        var destination = Account("2000000000", 300);

        var act = () => MoneyTransfer.Between(source, destination, Money.OfCents(501), null, OccurredAt);

        act.Should().Throw<InsufficientFundsForTransferException>();
        source.BalanceCents.Should().Be(500);
        destination.BalanceCents.Should().Be(300);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Between_rejects_a_non_positive_amount(long cents)
    {
        var source = Account("1000000000", 2000);
        var destination = Account("2000000000", 300);

        var act = () => MoneyTransfer.Between(source, destination, Money.OfCents(cents), null, OccurredAt);

        act.Should().Throw<InvalidTransferException>();
        source.BalanceCents.Should().Be(2000);
    }

    [Test]
    public void Between_treats_a_blank_reason_as_absent()
    {
        var transfer = MoneyTransfer.Between(
            Account("1000000000", 2000), Account("2000000000", 300), Money.OfCents(100), "   ", OccurredAt);

        transfer.Reason.Should().BeNull();
    }

    [Test]
    public void Between_refuses_accounts_in_different_currencies()
    {
        var source = Account("1000000000", 2000, currency: Currency.EUR);
        var destination = Account("2000000000", 300, currency: Currency.USD);

        var act = () => MoneyTransfer.Between(source, destination, Money.OfCents(100), null, OccurredAt);

        // No conversion exists, so this cannot be honoured rather than merely being unsupported.
        act.Should().Throw<InvalidTransferException>()
            .WithMessage("*different currencies*");
        source.BalanceCents.Should().Be(2000);
        destination.BalanceCents.Should().Be(300);
    }

    [Test]
    public void Between_refuses_an_amount_in_a_currency_the_accounts_do_not_hold()
    {
        var source = Account("1000000000", 2000, currency: Currency.EUR);
        var destination = Account("2000000000", 300, currency: Currency.EUR);

        var act = () => MoneyTransfer.Between(
            source, destination, Money.OfCents(100, Currency.USD), null, OccurredAt);

        act.Should().Throw<CurrencyMismatchException>();
        source.BalanceCents.Should().Be(2000);
    }

    [Test]
    public void Comparing_money_in_different_currencies_throws_rather_than_guessing()
    {
        var act = () => Money.OfCents(100, Currency.EUR) < Money.OfCents(100, Currency.USD);

        act.Should().Throw<IncompatibleCurrencyOperationException>();
    }

    private static Account Account(
        string number,
        long balanceCents,
        int id = 0,
        Currency currency = Currency.EUR) => new()
    {
        Id = id == 0 ? number.GetHashCode() & 0x7FFFFFFF : id,
        AccountNumber = number,
        Name = number,
        BalanceCents = balanceCents,
        Currency = currency,
        CreatedOn = DateTime.UtcNow
    };
}
