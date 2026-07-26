using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class V1TransactionReadTests
{
    [Test]
    public async Task Get_transaction_by_id_returns_the_direct_v1_transaction_resource()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedTransactions(factory);

        var response = await client.GetAsync($"/api/v1/transactions/{seeded.LatestTransactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transaction = document.RootElement;
        transaction.TryGetProperty("data", out _).Should().BeFalse();
        transaction.GetProperty("id").GetInt32().Should().Be(seeded.LatestTransactionId);
        transaction.GetProperty("type").GetString().Should().Be("income");
        transaction.GetProperty("amount").GetProperty("cents").GetInt64().Should().Be(1234);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("EUR");
        transaction.GetProperty("accountId").GetInt32().Should().Be(seeded.AccountId);
        transaction.GetProperty("categoryId").GetInt32().Should().Be(seeded.CategoryId);
        transaction.GetProperty("merchant").GetProperty("id").GetInt32().Should().Be(seeded.MerchantId);
        transaction.GetProperty("merchant").GetProperty("label").GetString().Should().Be("Grocer");
        transaction.GetProperty("tags").GetArrayLength().Should().Be(0);
        transaction.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
        transaction.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public async Task Get_transactions_returns_truthful_total_items_for_the_requested_page()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedTransactions(factory);

        var response = await client.GetAsync("/api/v1/transactions?page=2&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var page = document.RootElement;
        page.TryGetProperty("data", out _).Should().BeFalse();
        page.GetProperty("page").GetInt32().Should().Be(2);
        page.GetProperty("pageSize").GetInt32().Should().Be(2);
        page.GetProperty("totalItems").GetInt32().Should().Be(3);
        page.GetProperty("totalPages").GetInt32().Should().Be(2);
        var items = page.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("id").GetInt32().Should().Be(seeded.OldestTransactionId);
    }

    private static async Task<SeededTransactions> SeedTransactions(WebApiTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
        var account = new Account
        {
            Name = "Cash",
            AccountNumber = "1000000000",
            Balance = 0,
            CreatedOn = DateTime.UtcNow,
            IsDefaultAccount = true
        };
        var category = new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow };
        var merchant = new Merchant { Label = "Grocer", CreatedOn = DateTime.UtcNow };
        dbContext.AddRange(priority, account, category, merchant);
        await dbContext.SaveChangesAsync();

        var oldest = NewTransaction(500, TransactionType.Debit, new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero), account, category, merchant);
        var middle = NewTransaction(999, TransactionType.Debit, new DateTimeOffset(2026, 7, 26, 11, 0, 0, TimeSpan.Zero), account, category, merchant);
        var latest = NewTransaction(1234, TransactionType.Credit, new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero), account, category, merchant);
        dbContext.Transactions.AddRange(oldest, middle, latest);
        await dbContext.SaveChangesAsync();

        return new SeededTransactions(latest.Id, oldest.Id, account.Id, category.Id, merchant.Id);
    }

    private static Transaction NewTransaction(long amount, TransactionType type, DateTimeOffset occurredAt, Account account, Category category, Merchant merchant) => new()
    {
        Amount = amount,
        Currency = Currency.EUR,
        TransactionType = type,
        Account = account,
        Category = category,
        Merchant = merchant,
        Tags = [],
        CreatedOn = occurredAt.LocalDateTime
    };

    private sealed record SeededTransactions(int LatestTransactionId, int OldestTransactionId, int AccountId, int CategoryId, int MerchantId);
}
