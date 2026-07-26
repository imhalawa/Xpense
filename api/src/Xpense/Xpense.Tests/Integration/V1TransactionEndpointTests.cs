using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;
using Xpense.Services.Entities;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class V1TransactionEndpointTests
{
    [Test]
    public async Task Post_income_creates_a_direct_v1_resource_and_uses_the_get_by_id_location()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAccountAndCategory(factory, 0m);
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = "income",
            amount = new { cents = 1234, currency = "EUR" },
            accountNumber = seeded.AccountNumber,
            categoryId = seeded.CategoryId,
            merchant = new { label = "Employer", create = true },
            tags = new[] { new { label = "salary", create = true } },
            occurredAt
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().MatchRegex("/api/v1/transactions/[1-9][0-9]*$");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transaction = document.RootElement;
        transaction.TryGetProperty("data", out _).Should().BeFalse();
        transaction.GetProperty("type").GetString().Should().Be("income");
        transaction.GetProperty("amount").GetProperty("cents").GetInt64().Should().Be(1234);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("EUR");
        transaction.GetProperty("accountId").GetInt32().Should().Be(seeded.AccountId);
        transaction.GetProperty("categoryId").GetInt32().Should().Be(seeded.CategoryId);
        transaction.GetProperty("merchant").GetProperty("label").GetString().Should().Be("Employer");
        transaction.GetProperty("tags").GetArrayLength().Should().Be(1);
        transaction.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(occurredAt);

        var createdId = transaction.GetProperty("id").GetInt32();
        var getCreatedResponse = await client.GetAsync(response.Headers.Location);
        getCreatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getCreatedDocument = JsonDocument.Parse(await getCreatedResponse.Content.ReadAsStringAsync());
        getCreatedDocument.RootElement.GetProperty("id").GetInt32().Should().Be(createdId);

        (await GetAccountBalance(factory, seeded.AccountId)).Should().Be(12.34m);
    }

    [Test]
    public async Task Post_expense_creates_a_direct_v1_resource_and_debits_exact_cents()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var seeded = await SeedAccountAndCategory(factory, 20m);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = "expense",
            amount = new { cents = 99, currency = "USD" },
            accountNumber = seeded.AccountNumber,
            categoryId = seeded.CategoryId,
            merchant = new { label = "Coffee Shop", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transaction = document.RootElement;
        transaction.GetProperty("type").GetString().Should().Be("expense");
        transaction.GetProperty("amount").GetProperty("cents").GetInt64().Should().Be(99);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("USD");
        transaction.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);

        (await GetAccountBalance(factory, seeded.AccountId)).Should().Be(19.01m);
    }

    [TestCase("transfer")]
    [TestCase("refund")]
    public async Task Post_with_an_unsupported_type_returns_a_validation_problem(string type)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type,
            amount = new { cents = 1234, currency = "EUR" },
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errors").TryGetProperty("type", out _).Should().BeTrue();
    }

    [TestCase(0, "EUR")]
    [TestCase(1234, "0")]
    [TestCase(1234, "GBP")]
    public async Task Post_with_an_invalid_amount_returns_a_validation_problem(long cents, string currency)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = "income",
            amount = new { cents, currency },
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [TestCase("/api/transaction/deposit")]
    [TestCase("/api/transaction/withdraw")]
    [TestCase("/api/transaction/transfer")]
    public async Task Removed_or_unimplemented_transaction_routes_return_not_found(string path)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(path, JsonContent.Create(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<SeededAccountAndCategory> SeedAccountAndCategory(WebApiTestFactory factory, decimal balance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
        var account = new Account
        {
            Name = "Cash",
            AccountNumber = "1000000000",
            Balance = balance,
            CreatedOn = DateTime.UtcNow,
            IsDefaultAccount = true
        };
        var category = new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow };
        dbContext.AddRange(priority, account, category);
        await dbContext.SaveChangesAsync();

        return new SeededAccountAndCategory(account.Id, account.AccountNumber, category.Id);
    }

    private static async Task<decimal> GetAccountBalance(WebApiTestFactory factory, int accountId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        return await dbContext.Accounts.AsNoTracking().Where(account => account.Id == accountId).Select(account => account.Balance).SingleAsync();
    }

    private sealed record SeededAccountAndCategory(int AccountId, string AccountNumber, int CategoryId);
}
