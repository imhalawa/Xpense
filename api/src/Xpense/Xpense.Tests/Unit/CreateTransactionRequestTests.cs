using FluentAssertions;
using Xpense.API.Models.Requests;
using Xpense.Services.Enums;

namespace Xpense.Tests.Unit;

[TestFixture]
public class CreateTransactionRequestTests
{
    [Test]
    public void Income_maps_public_values_to_a_deposit_command()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);
        var request = new CreateTransactionRequest
        {
            Type = "income",
            Amount = new TransactionMoney(1234, "EUR"),
            AccountNumber = "1234567890",
            CategoryId = 7,
            Merchant = new TransactionMerchant(4, "Employer", false),
            Tags = [new TransactionTag(2, "salary", false)],
            OccurredAt = occurredAt
        };

        request.TryGetTransactionType(out var kind).Should().BeTrue();
        kind.Should().Be(TransactionType.Credit);

        var command = request.ToDepositCommand();
        command.Amount.Cents.Should().Be(1234);
        command.Amount.Currency.Should().Be(Currency.EUR);
        command.AccountNumber.Should().Be("1234567890");
        command.CategoryId.Should().Be(7);
        command.Merchant.Id.Should().Be(4);
        command.Merchant.Label.Should().Be("Employer");
        command.Merchant.Create.Should().BeFalse();
        command.Tags.Should().ContainSingle();
        command.Tags![0].Id.Should().Be(2);
        command.Tags[0].Label.Should().Be("salary");
        command.CreatedOn.Should().Be(occurredAt.ToUnixTimeSeconds());
    }

    [Test]
    public void Expense_maps_optional_account_and_timestamp_to_a_withdraw_command()
    {
        var request = new CreateTransactionRequest
        {
            Type = "expense",
            Amount = new TransactionMoney(99, "USD"),
            CategoryId = 3,
            Merchant = new TransactionMerchant(null, "Coffee Shop", true)
        };

        request.TryGetTransactionType(out var kind).Should().BeTrue();
        kind.Should().Be(TransactionType.Debit);

        var command = request.ToWithdrawCommand();
        command.Amount.Cents.Should().Be(99);
        command.Amount.Currency.Should().Be(Currency.USD);
        command.AccountNumber.Should().BeNull();
        command.CategoryId.Should().Be(3);
        command.Merchant.Id.Should().BeNull();
        command.Merchant.Label.Should().Be("Coffee Shop");
        command.Merchant.Create.Should().BeTrue();
        command.Tags.Should().BeNull();
        command.CreatedOn.Should().BeNull();
    }

    [TestCase("transfer")]
    [TestCase("refund")]
    public void Unsupported_type_does_not_classify_as_a_transaction_kind(string type)
    {
        var request = new CreateTransactionRequest { Type = type };

        request.TryGetTransactionType(out _).Should().BeFalse();
    }
}
