using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class TransferEndpointTests
{
    [Test]
    public async Task Post_creates_an_atomic_transfer_resource_with_two_auditable_legs()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var accounts = await SeedAccounts(factory, 20m, 3m);
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = accounts.DestinationId,
            amount = new { cents = 1234, currency = "USD" },
            reason = "Shared rent",
            occurredAt
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transfer = document.RootElement;
        transfer.TryGetProperty("data", out _).Should().BeFalse();
        transfer.GetProperty("id").GetInt32().Should().BePositive();
        transfer.GetProperty("sourceAccountId").GetInt32().Should().Be(accounts.SourceId);
        transfer.GetProperty("destinationAccountId").GetInt32().Should().Be(accounts.DestinationId);
        transfer.GetProperty("amount").GetProperty("cents").GetInt64().Should().Be(1234);
        transfer.GetProperty("amount").GetProperty("currency").GetString().Should().Be("USD");
        transfer.GetProperty("reason").GetString().Should().Be("Shared rent");
        transfer.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(occurredAt);
        transfer.GetProperty("legs").EnumerateArray()
            .Select(leg => leg.GetProperty("direction").GetString())
            .Should().BeEquivalentTo("debit", "credit");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var persisted = await dbContext.Transfers.AsNoTracking()
            .Include(item => item.Legs)
            .SingleAsync(item => item.Id == transfer.GetProperty("id").GetInt32());
        (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.SourceId))
            .Balance.Should().Be(7.66m);
        (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.DestinationId))
            .Balance.Should().Be(15.34m);
        persisted.Legs.Should().HaveCount(2)
            .And.OnlyContain(leg => leg.TransferId == persisted.Id && leg.Amount == 1234 && leg.Currency == Currency.USD);
    }

    [Test]
    public async Task Post_with_identical_accounts_returns_a_validation_problem_without_writes()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var accounts = await SeedAccounts(factory, 20m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = accounts.SourceId,
            amount = new { cents = 100, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errors").TryGetProperty("destinationAccountId", out _).Should().BeTrue();
        await AssertUnchanged(factory, accounts, 20m, 3m);
    }

    [Test]
    public async Task Post_when_source_has_insufficient_funds_returns_a_validation_problem_without_writes()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var accounts = await SeedAccounts(factory, 5m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = accounts.DestinationId,
            amount = new { cents = 501, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        await AssertUnchanged(factory, accounts, 5m, 3m);
    }

    [TestCase(0, "EUR")]
    [TestCase(100, "GBP")]
    public async Task Post_with_invalid_money_returns_a_validation_problem(long cents, string currency)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = 1,
            destinationAccountId = 2,
            amount = new { cents, currency }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Test]
    public async Task Post_with_a_missing_account_returns_not_found_without_writes()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var accounts = await SeedAccounts(factory, 20m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = 999,
            amount = new { cents = 100, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        await AssertUnchanged(factory, accounts, 20m, 3m);
    }

    private static async Task<TransferAccounts> SeedAccounts(
        WebApiTestFactory factory,
        decimal sourceBalance,
        decimal destinationBalance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var source = Account("1000000000", "Source", sourceBalance);
        var destination = Account("2000000000", "Destination", destinationBalance);
        dbContext.Accounts.AddRange(source, destination);
        await dbContext.SaveChangesAsync();
        return new TransferAccounts(source.Id, destination.Id);
    }

    private static async Task AssertUnchanged(
        WebApiTestFactory factory,
        TransferAccounts accounts,
        decimal sourceBalance,
        decimal destinationBalance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.SourceId))
            .Balance.Should().Be(sourceBalance);
        (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.DestinationId))
            .Balance.Should().Be(destinationBalance);
        (await dbContext.Transfers.CountAsync()).Should().Be(0);
    }

    private static Account Account(string number, string name, decimal balance)
    {
        return new Account
        {
            AccountNumber = number,
            Name = name,
            Balance = balance,
            CreatedOn = DateTime.UtcNow
        };
    }

    private sealed record TransferAccounts(int SourceId, int DestinationId);
}
