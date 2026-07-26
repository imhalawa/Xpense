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
public class V1AnalyticsEndpointTests
{
    [Test]
    public async Task Get_spending_by_category_returns_the_current_summary_directly()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedTodayExpense(factory);

        var response = await client.GetAsync("/api/v1/analytics/spending/by-category");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = document.RootElement;
        summary.TryGetProperty("data", out _).Should().BeFalse();
        summary.GetProperty("total").GetProperty("cents").GetInt64().Should().Be(1250);
        summary.GetProperty("total").GetProperty("currency").GetString().Should().Be("EUR");
        var expenses = summary.GetProperty("expenses");
        expenses.GetArrayLength().Should().Be(1);
        expenses[0].GetProperty("category").GetProperty("label").GetString().Should().Be("Food");
        expenses[0].GetProperty("amount").GetProperty("cents").GetInt64().Should().Be(1250);
    }

    [Test]
    public async Task Legacy_today_categories_route_is_not_available()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/analytics/today/categories");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task SeedTodayExpense(WebApiTestFactory factory)
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

        dbContext.Transactions.Add(new Transaction
        {
            Amount = 1250,
            Currency = Currency.EUR,
            TransactionType = TransactionType.Debit,
            Account = account,
            Category = category,
            Merchant = merchant,
            Tags = [],
            CreatedOn = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}
