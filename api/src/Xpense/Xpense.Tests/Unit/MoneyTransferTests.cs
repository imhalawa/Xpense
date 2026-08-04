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
        var source = Account("1000000000", 20m);
        var destination = Account("2000000000", 3m);

        var transfer = MoneyTransfer.Between(
            source, destination, Money.OfCents(1234, Currency.USD), "Shared rent", OccurredAt);

        source.Balance.Should().Be(7.66m);
        destination.Balance.Should().Be(15.34m);
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
        var account = Account("1000000000", 20m);
        account.Id = 7;

        var act = () => MoneyTransfer.Between(account, account, Money.OfCents(100), null, OccurredAt);

        act.Should().Throw<InvalidTransferException>();
        account.Balance.Should().Be(20m);
    }

    [Test]
    public void Between_rejects_an_amount_beyond_the_source_balance_without_moving_money()
    {
        var source = Account("1000000000", 5m);
        var destination = Account("2000000000", 3m);

        var act = () => MoneyTransfer.Between(source, destination, Money.OfCents(501), null, OccurredAt);

        act.Should().Throw<InsufficientFundsForTransferException>();
        source.Balance.Should().Be(5m);
        destination.Balance.Should().Be(3m);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Between_rejects_a_non_positive_amount(long cents)
    {
        var source = Account("1000000000", 20m);
        var destination = Account("2000000000", 3m);

        var act = () => MoneyTransfer.Between(source, destination, Money.OfCents(cents), null, OccurredAt);

        act.Should().Throw<InvalidTransferException>();
        source.Balance.Should().Be(20m);
    }

    [Test]
    public void Between_treats_a_blank_reason_as_absent()
    {
        var transfer = MoneyTransfer.Between(
            Account("1000000000", 20m), Account("2000000000", 3m), Money.OfCents(100), "   ", OccurredAt);

        transfer.Reason.Should().BeNull();
    }

    private static Account Account(string number, decimal balance, int id = 0) => new()
    {
        Id = id == 0 ? number.GetHashCode() & 0x7FFFFFFF : id,
        AccountNumber = number,
        Name = number,
        Balance = balance,
        CreatedOn = DateTime.UtcNow
    };
}
