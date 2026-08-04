using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Enums;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

/// <summary>
/// The canonical HTTP contract suite. Every endpoint, every error shape, one file.
/// Each test gets a fresh in-memory database via <see cref="WebApiTestFactory"/>.
/// </summary>
[TestFixture]
public class ApiEndpointTests
{
    private const string ProblemJson = "application/problem+json";

    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        factory = new WebApiTestFactory();
        client = factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        client?.Dispose();
        factory?.Dispose();
    }

    // ---------------------------------------------------------------- collections

    [TestCase("/api/v1/accounts", "Cash")]
    [TestCase("/api/v1/categories", "Food")]
    [TestCase("/api/v1/tags", "Travel")]
    [TestCase("/api/v1/merchants", "Albert Heijn")]
    public async Task Get_resource_collections_use_plural_routes_and_return_resources_directly(
        string route,
        string expectedLabel)
    {
        await SeedOneOfEachResource();

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("label").GetString().Should().Be(expectedLabel);
    }

    // ---------------------------------------------------------------- accounts

    [Test]
    public async Task Post_accounts_returns_the_created_resource_at_its_id_route()
    {
        await SeedAccount();

        var response = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"name\":\"Savings\",\"balance\":123.45}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/accounts/2"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(2);
        document.RootElement.GetProperty("label").GetString().Should().Be("Savings");
    }

    [Test]
    public async Task Get_accounts_by_id_returns_the_direct_resource()
    {
        await SeedAccount();

        var response = await client.GetAsync("/api/v1/accounts/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Cash");
    }

    [Test]
    public async Task Put_accounts_updates_the_resource_and_delete_returns_no_content()
    {
        await SeedAccount();

        var updateResponse = await client.PutAsync(
            "/api/v1/accounts/1",
            JsonBody("{\"name\":\"Updated Cash\",\"isDefault\":false}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/accounts/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Updated Cash");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------------------------------------------------------------- categories

    [Test]
    public async Task Post_categories_returns_the_created_resource_at_its_id_route()
    {
        await SeedPriority();

        var response = await client.PostAsync(
            "/api/v1/categories",
            JsonBody("{\"name\":\"Food\",\"priorityId\":1}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/categories/1"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Food");
    }

    [Test]
    public async Task Get_categories_by_id_returns_the_direct_resource()
    {
        await SeedCategory();

        var response = await client.GetAsync("/api/v1/categories/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Food");
    }

    [Test]
    public async Task Put_categories_updates_the_resource_and_delete_returns_no_content()
    {
        await SeedCategory();

        var updateResponse = await client.PutAsync(
            "/api/v1/categories/1",
            JsonBody("{\"name\":\"Dining\",\"priorityId\":1}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/categories/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Dining");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------------------------------------------------------------- tags

    [Test]
    public async Task Post_tags_returns_the_created_resource_at_its_id_route()
    {
        var response = await client.PostAsync("/api/v1/tags", NewTagBody("Travel"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/tags/1"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Travel");

        var getResponse = await client.GetAsync(response.Headers.Location);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Delete_tags_uses_the_resource_id_route_and_returns_no_content()
    {
        var createResponse = await client.PostAsync("/api/v1/tags", NewTagBody("Travel"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/tags/1"));

        var response = await client.DeleteAsync(createResponse.Headers.Location);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Put_tags_updates_the_resource_and_delete_returns_no_content()
    {
        var createResponse = await client.PostAsync("/api/v1/tags", NewTagBody("Travel"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // "label" on update as well as create -- it used to be "name" on update only.
        var updateResponse = await client.PutAsync(
            "/api/v1/tags/1",
            JsonBody("{\"label\":\"Holiday\",\"bgColorHex\":\"#123456\",\"fgColorHex\":\"#abcdef\"}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/tags/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Holiday");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------------------------------------------------------------- transactions

    [Test]
    public async Task Post_income_creates_a_direct_resource_and_uses_the_get_by_id_location()
    {
        var seeded = await SeedAccountAndCategory(0m);
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

        (await GetAccountBalance(seeded.AccountId)).Should().Be(12.34m);
    }

    [Test]
    public async Task Post_expense_creates_a_direct_resource_and_debits_exact_cents()
    {
        var seeded = await SeedAccountAndCategory(20m);

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

        (await GetAccountBalance(seeded.AccountId)).Should().Be(19.01m);
    }

    [Test]
    public async Task Get_transaction_by_id_returns_the_direct_transaction_resource()
    {
        var seeded = await SeedTransactions();

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
        transaction.GetProperty("occurredAt").GetDateTimeOffset()
            .Should().Be(new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero));
        transaction.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public async Task Get_transactions_returns_truthful_total_items_for_the_requested_page()
    {
        var seeded = await SeedTransactions();

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

    [Test]
    public async Task Get_transactions_without_query_params_pages_by_default()
    {
        // Used to bind page/pageSize to 0, fail FilterQuery.IsValid() and surface as a 500.
        var response = await client.GetAsync("/api/v1/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("pageSize").GetInt32().Should().Be(25);
        document.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [TestCase("transfer")]
    [TestCase("refund")]
    public async Task Post_transaction_with_an_unsupported_type_returns_a_validation_problem(string type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type,
            amount = new { cents = 1234, currency = "EUR" },
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errors").TryGetProperty("type", out _).Should().BeTrue();
    }

    [TestCase(0, "EUR")]
    [TestCase(1234, "0")]
    [TestCase(1234, "GBP")]
    public async Task Post_transaction_with_an_invalid_amount_returns_a_validation_problem(long cents, string currency)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = "income",
            amount = new { cents, currency },
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    // ---------------------------------------------------------------- transfers

    [Test]
    public async Task Post_creates_an_atomic_transfer_resource_with_two_auditable_legs()
    {
        var accounts = await SeedTransferAccounts(20m, 3m);
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
    public async Task Post_transfer_with_identical_accounts_is_rejected_without_writes()
    {
        var accounts = await SeedTransferAccounts(20m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = accounts.SourceId,
            amount = new { cents = 100, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement
            .GetProperty("errors")
            .GetProperty("destinationAccountId")[0]
            .GetString()
            .Should().Be("Source and destination accounts must be different.");
        await AssertBalancesUnchanged(accounts, 20m, 3m);
    }

    [Test]
    public async Task Post_transfer_with_insufficient_funds_is_rejected_without_writes()
    {
        var accounts = await SeedTransferAccounts(5m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = accounts.DestinationId,
            amount = new { cents = 501, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be("Insufficient funds");
        document.RootElement.GetProperty("errors").TryGetProperty("amount.cents", out _).Should().BeTrue();
        await AssertBalancesUnchanged(accounts, 5m, 3m);
    }

    [TestCase(0, "EUR")]
    [TestCase(100, "GBP")]
    public async Task Post_transfer_with_invalid_money_returns_a_validation_problem(long cents, string currency)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = 1,
            destinationAccountId = 2,
            amount = new { cents, currency }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    [Test]
    public async Task Post_transfer_to_a_missing_account_returns_problem_details_without_writes()
    {
        var accounts = await SeedTransferAccounts(20m, 3m);

        var response = await client.PostAsJsonAsync("/api/v1/transfers", new
        {
            sourceAccountId = accounts.SourceId,
            destinationAccountId = 999,
            amount = new { cents = 100, currency = "EUR" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be("Resource not found");
        await AssertBalancesUnchanged(accounts, 20m, 3m);
    }

    // ---------------------------------------------------------------- analytics

    [Test]
    public async Task Get_spending_by_category_returns_the_current_summary_directly()
    {
        await SeedTodayExpense();

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

    // ---------------------------------------------------------------- error contract
    //
    // Before the exception handlers landed the API produced four different error shapes: two
    // envelope variants (one camelCase, one PascalCase), bare 404s with no body, and real
    // problem details. Nothing caught it because tests only asserted status codes.

    [Test]
    public async Task Unknown_account_returns_problem_details_not_an_envelope()
    {
        var response = await client.GetAsync("/api/v1/accounts/424242");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("title").GetString().Should().Be("Resource not found");
        root.GetProperty("detail").GetString().Should().Contain("424242");

        // The old Response<T> envelope leaked these two keys; they must never come back.
        root.TryGetProperty("statusCode", out _).Should().BeFalse();
        root.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Test]
    public async Task Unknown_transaction_returns_problem_details_with_a_detail_message()
    {
        var response = await client.GetAsync("/api/v1/transactions/424242");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);

        // Previously a bare NotFoundResult with an empty body.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("detail").GetString().Should().Contain("424242");
    }

    [Test]
    public async Task Invalid_paging_reports_a_domain_rule_violation_as_400()
    {
        var response = await client.GetAsync("/api/v1/transactions?page=0&pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be("Request breaks a domain rule");
    }

    [Test]
    public async Task Blank_account_name_is_reported_as_a_camel_case_field_error()
    {
        var response = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"name\":\"\",\"balance\":10}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement
            .GetProperty("errors")
            .GetProperty("name")[0]
            .GetString()
            .Should().Be("The name is required.");
    }

    [Test]
    public async Task Malformed_tag_colour_is_reported_against_the_camel_case_field_name()
    {
        var response = await client.PostAsync(
            "/api/v1/tags",
            JsonBody("{\"label\":\"Travel\",\"bgColorHex\":\"not-a-colour\",\"fgColorHex\":\"#000000\"}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // FluentValidation reports "BgColorHex"; the contract is camelCase.
        document.RootElement.GetProperty("errors")
            .TryGetProperty("bgColorHex", out var colourErrors).Should().BeTrue();
        colourErrors[0].GetString().Should().Contain("hex colour");
    }

    // ---------------------------------------------------------------- removed legacy routes

    [TestCase("/api/category")]
    [TestCase("/api/tag")]
    [TestCase("/api/merchant")]
    public async Task Legacy_singular_resource_routes_are_not_available(string route)
    {
        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestCase("/api/account")]
    [TestCase("/api/account/1000000000")]
    [TestCase("/api/v1/accounts/1000000000")]
    public async Task Legacy_account_number_put_routes_are_not_available(string route)
    {
        var response = await client.PutAsync(route, JsonBody("{\"name\":\"Cash\",\"isDefault\":true}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestCase("/api/transaction/deposit")]
    [TestCase("/api/transaction/withdraw")]
    [TestCase("/api/transaction/transfer")]
    public async Task Removed_or_unimplemented_transaction_routes_return_not_found(string path)
    {
        var response = await client.PostAsync(path, JsonContent.Create(new { }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Legacy_today_categories_route_is_not_available()
    {
        var response = await client.GetAsync("/api/analytics/today/categories");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- helpers

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    private static StringContent NewTagBody(string label) =>
        JsonBody($"{{\"label\":\"{label}\",\"bgColorHex\":\"#ffffff\",\"fgColorHex\":\"#000000\"}}");

    private XpenseDbContext NewDbContext(out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
    }

    private static Account NewAccount(string number, string name, decimal balance, bool isDefault = false) => new()
    {
        AccountNumber = number,
        Name = name,
        Balance = balance,
        CreatedOn = DateTime.UtcNow,
        IsDefaultAccount = isDefault
    };

    private async Task SeedAccount()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            dbContext.Accounts.Add(NewAccount("1000000000", "Cash", 0, isDefault: true));
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedPriority()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            dbContext.Priorities.Add(new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedCategory()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
            dbContext.Priorities.Add(priority);
            dbContext.Categories.Add(new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// One row per collection resource, so the collection assertions are real. Previously only
    /// an account was seeded and the category/tag/merchant cases passed against an empty array.
    /// </summary>
    private async Task SeedOneOfEachResource()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            // Link via navigation, not a hard-coded PriorityId, so EF orders the inserts.
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
            dbContext.Priorities.Add(priority);
            dbContext.Accounts.Add(NewAccount("1000000000", "Cash", 0, isDefault: true));
            dbContext.Categories.Add(new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow });
            dbContext.Tags.Add(new Tag { Label = "Travel", CreatedOn = DateTime.UtcNow });
            dbContext.Merchants.Add(new Merchant { Label = "Albert Heijn", CreatedOn = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<SeededAccountAndCategory> SeedAccountAndCategory(decimal balance)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
            var account = NewAccount("1000000000", "Cash", balance, isDefault: true);
            var category = new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow };
            dbContext.AddRange(priority, account, category);
            await dbContext.SaveChangesAsync();

            return new SeededAccountAndCategory(account.Id, account.AccountNumber, category.Id);
        }
    }

    private async Task<SeededTransactions> SeedTransactions()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
            var account = NewAccount("1000000000", "Cash", 0, isDefault: true);
            var category = new Category { Label = "Food", Priority = priority, CreatedOn = DateTime.UtcNow };
            var merchant = new Merchant { Label = "Grocer", CreatedOn = DateTime.UtcNow };
            dbContext.AddRange(priority, account, category, merchant);
            await dbContext.SaveChangesAsync();

            var oldest = NewTransaction(500, TransactionType.Debit, At(10), account, category, merchant);
            var middle = NewTransaction(999, TransactionType.Debit, At(11), account, category, merchant);
            var latest = NewTransaction(1234, TransactionType.Credit, At(12), account, category, merchant);
            dbContext.Transactions.AddRange(oldest, middle, latest);
            await dbContext.SaveChangesAsync();

            return new SeededTransactions(latest.Id, oldest.Id, account.Id, category.Id, merchant.Id);
        }

        static DateTimeOffset At(int hour) => new(2026, 7, 26, hour, 0, 0, TimeSpan.Zero);
    }

    private async Task SeedTodayExpense()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedOn = DateTime.UtcNow };
            var account = NewAccount("1000000000", "Cash", 0, isDefault: true);
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

    private async Task<TransferAccounts> SeedTransferAccounts(decimal sourceBalance, decimal destinationBalance)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var source = NewAccount("1000000000", "Source", sourceBalance);
            var destination = NewAccount("2000000000", "Destination", destinationBalance);
            dbContext.Accounts.AddRange(source, destination);
            await dbContext.SaveChangesAsync();
            return new TransferAccounts(source.Id, destination.Id);
        }
    }

    private static Transaction NewTransaction(
        long amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        Account account,
        Category category,
        Merchant merchant) => new()
    {
        Amount = amount,
        Currency = Currency.EUR,
        TransactionType = type,
        Account = account,
        Category = category,
        Merchant = merchant,
        Tags = [],
        CreatedOn = occurredAt.UtcDateTime
    };

    private async Task<decimal> GetAccountBalance(int accountId)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            return await dbContext.Accounts.AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => account.Balance)
                .SingleAsync();
        }
    }

    private async Task AssertBalancesUnchanged(
        TransferAccounts accounts,
        decimal sourceBalance,
        decimal destinationBalance)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.SourceId))
                .Balance.Should().Be(sourceBalance);
            (await dbContext.Accounts.AsNoTracking().SingleAsync(account => account.Id == accounts.DestinationId))
                .Balance.Should().Be(destinationBalance);
            (await dbContext.Transfers.CountAsync()).Should().Be(0);
        }
    }

    private sealed record SeededAccountAndCategory(int AccountId, string AccountNumber, int CategoryId);

    private sealed record SeededTransactions(
        int LatestTransactionId,
        int OldestTransactionId,
        int AccountId,
        int CategoryId,
        int MerchantId);

    private sealed record TransferAccounts(int SourceId, int DestinationId);
}
