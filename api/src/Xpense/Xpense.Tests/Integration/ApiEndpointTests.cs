using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.ValueObjects;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

/// <summary>
/// The canonical HTTP contract suite. Every endpoint, every error shape, one file.
/// Each test gets its own Postgres database, cloned from the migrated template that
/// <see cref="PostgresFixture"/> builds once per run.
/// </summary>
[TestFixture]
public class ApiEndpointTests
{
    private const string ProblemJson = "application/problem+json";
    private const string SourceNumber = "1000000000";
    private const string DestinationNumber = "2000000000";

    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase());
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
    public async Task Post_accounts_returns_the_created_resource_at_its_account_number_route()
    {
        await SeedAccount();

        var response = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"label\":\"Savings\",\"balance\":{\"minorUnits\":12345,\"currency\":\"EUR\"}}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/accounts/1000000001"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("accountNumber").GetString().Should().Be("1000000001");
        document.RootElement.GetProperty("label").GetString().Should().Be("Savings");

        // The database key is not part of the contract, so it must not leak into the body.
        document.RootElement.TryGetProperty("id", out _).Should().BeFalse();
    }

    [Test]
    public async Task Get_accounts_by_number_returns_the_direct_resource()
    {
        await SeedAccount();

        var response = await client.GetAsync($"/api/v1/accounts/{SourceNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("accountNumber").GetString().Should().Be(SourceNumber);
        document.RootElement.GetProperty("label").GetString().Should().Be("Cash");
    }

    [Test]
    public async Task Put_accounts_updates_the_resource_and_delete_returns_no_content()
    {
        await SeedAccount();

        var updateResponse = await client.PutAsync(
            $"/api/v1/accounts/{SourceNumber}",
            JsonBody("{\"label\":\"Updated Cash\",\"isDefault\":false}"));
        var deleteResponse = await client.DeleteAsync($"/api/v1/accounts/{SourceNumber}");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Updated Cash");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Account_timestamps_are_iso_8601_not_unix_seconds()
    {
        await SeedAccount();

        var response = await client.GetAsync($"/api/v1/accounts/{SourceNumber}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createdAt = document.RootElement.GetProperty("createdAt");
        createdAt.ValueKind.Should().Be(JsonValueKind.String);
        createdAt.GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        document.RootElement.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------------------------------------------------------------- categories

    [Test]
    public async Task Post_categories_returns_the_created_resource_at_its_id_route()
    {
        await SeedPriority();

        var response = await client.PostAsync(
            "/api/v1/categories",
            JsonBody("{\"label\":\"Food\",\"priorityId\":1}"));

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
            JsonBody("{\"label\":\"Dining\",\"priorityId\":1}"));
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
    //
    // One resource, three kinds. Which sides the caller names decides the kind, so there is no
    // type field: only a destination is income, only a source is expense, both is a transfer.

    [Test]
    public async Task Post_income_creates_a_direct_resource_and_uses_the_get_by_id_location()
    {
        var seeded = await SeedAccountAndCategory(0);
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 1234, currency = "EUR" },
            destinationAccountNumber = seeded.AccountNumber,
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
        transaction.GetProperty("kind").GetString().Should().Be("income");
        transaction.GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(1234);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("EUR");
        transaction.GetProperty("destinationAccountNumber").GetString().Should().Be(seeded.AccountNumber);
        transaction.GetProperty("sourceAccountNumber").ValueKind.Should().Be(JsonValueKind.Null);
        transaction.GetProperty("categoryId").GetInt32().Should().Be(seeded.CategoryId);
        transaction.GetProperty("merchant").GetProperty("label").GetString().Should().Be("Employer");
        transaction.GetProperty("tags").GetArrayLength().Should().Be(1);
        transaction.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(occurredAt);

        var createdId = transaction.GetProperty("id").GetInt32();
        var getCreatedResponse = await client.GetAsync(response.Headers.Location);
        getCreatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getCreatedDocument = JsonDocument.Parse(await getCreatedResponse.Content.ReadAsStringAsync());
        getCreatedDocument.RootElement.GetProperty("id").GetInt32().Should().Be(createdId);

        (await GetAccountBalance(seeded.AccountNumber)).Should().Be(1234);
    }

    /// <summary>
    /// OccurredAt and CreatedAt are separate facts. A transaction dated in the past keeps that date
    /// while still recording when the row was written -- they were the same column until now, so a
    /// backdated entry made it impossible to tell when anything was entered.
    /// </summary>
    [Test]
    public async Task Post_transaction_keeps_the_supplied_occurrence_time_and_stamps_its_own_created_time()
    {
        var seeded = await SeedAccountAndCategory(5000);
        var occurredAt = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 250, currency = "EUR" },
            sourceAccountNumber = seeded.AccountNumber,
            categoryId = seeded.CategoryId,
            merchant = new { label = "Coffee Shop", create = true },
            occurredAt
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(occurredAt);
        document.RootElement.GetProperty("createdAt").GetDateTimeOffset()
            .Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task Post_expense_creates_a_direct_resource_and_debits_exact_minor_units()
    {
        var seeded = await SeedAccountAndCategory(2000);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 99, currency = "EUR" },
            sourceAccountNumber = seeded.AccountNumber,
            categoryId = seeded.CategoryId,
            merchant = new { label = "Coffee Shop", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transaction = document.RootElement;
        transaction.GetProperty("kind").GetString().Should().Be("expense");
        transaction.GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(99);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("EUR");
        transaction.GetProperty("sourceAccountNumber").GetString().Should().Be(seeded.AccountNumber);
        transaction.GetProperty("destinationAccountNumber").ValueKind.Should().Be(JsonValueKind.Null);
        transaction.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);

        (await GetAccountBalance(seeded.AccountNumber)).Should().Be(1901);
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
        transaction.GetProperty("kind").GetString().Should().Be("income");
        transaction.GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(1234);
        transaction.GetProperty("amount").GetProperty("currency").GetString().Should().Be("EUR");
        transaction.GetProperty("destinationAccountNumber").GetString().Should().Be(SourceNumber);
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

    [Test]
    public async Task Post_transaction_naming_neither_account_is_rejected()
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 1234, currency = "EUR" },
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errors")
            .TryGetProperty("sourceAccountNumber", out _).Should().BeTrue();
    }

    [Test]
    public async Task Post_one_sided_transaction_without_a_category_or_merchant_is_rejected()
    {
        var seeded = await SeedAccountAndCategory(2000);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = seeded.AccountNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        errors.TryGetProperty("categoryId", out _).Should().BeTrue();
        errors.TryGetProperty("merchant", out _).Should().BeTrue();
    }

    [Test]
    public async Task Post_transfer_carrying_a_category_or_merchant_is_rejected()
    {
        var accounts = await SeedTransferAccounts(2000, 300);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber,
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        errors.TryGetProperty("categoryId", out _).Should().BeTrue();
        errors.TryGetProperty("merchant", out _).Should().BeTrue();
        await AssertBalancesUnchanged(accounts, 2000, 300);
    }

    [TestCase(0, "EUR")]
    [TestCase(1234, "0")]
    [TestCase(1234, "GBP")]
    public async Task Post_transaction_with_an_invalid_amount_returns_a_validation_problem(
        long minorUnits,
        string currency)
    {
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits, currency },
            destinationAccountNumber = SourceNumber,
            categoryId = 1,
            merchant = new { label = "Employer", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    // ---------------------------------------------------------------- transfers
    //
    // A transfer is a transaction with both sides named. There is no /transfers resource and no
    // leg rows: the legs held nothing the parent did not already hold.

    [Test]
    public async Task Post_creates_an_atomic_transfer_naming_both_accounts()
    {
        // Both accounts in USD: proves a non-default currency works end to end.
        var accounts = await SeedTransferAccounts(2000, 300, Currency.USD, Currency.USD);
        var occurredAt = new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 1234, currency = "USD" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber,
            reason = "Shared rent",
            occurredAt
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transfer = document.RootElement;
        transfer.TryGetProperty("data", out _).Should().BeFalse();
        transfer.GetProperty("id").GetInt32().Should().BePositive();
        transfer.GetProperty("kind").GetString().Should().Be("transfer");
        transfer.GetProperty("sourceAccountNumber").GetString().Should().Be(accounts.SourceNumber);
        transfer.GetProperty("destinationAccountNumber").GetString().Should().Be(accounts.DestinationNumber);
        transfer.GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(1234);
        transfer.GetProperty("amount").GetProperty("currency").GetString().Should().Be("USD");
        transfer.GetProperty("reason").GetString().Should().Be("Shared rent");
        transfer.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(occurredAt);

        // A transfer is money you still own, so it has no spending class and no external party.
        transfer.GetProperty("categoryId").ValueKind.Should().Be(JsonValueKind.Null);
        transfer.GetProperty("merchant").ValueKind.Should().Be(JsonValueKind.Null);

        (await GetAccountBalance(accounts.SourceNumber)).Should().Be(766);
        (await GetAccountBalance(accounts.DestinationNumber)).Should().Be(1534);
    }

    [Test]
    public async Task Post_transfer_appears_in_the_transaction_list()
    {
        var accounts = await SeedTransferAccounts(2000, 300);

        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber
        });

        var response = await client.GetAsync("/api/v1/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("kind").GetString().Should().Be("transfer");
    }

    [Test]
    public async Task Post_transfer_with_identical_accounts_is_rejected_without_writes()
    {
        var accounts = await SeedTransferAccounts(2000, 300);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.SourceNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement
            .GetProperty("errors")
            .GetProperty("destinationAccountNumber")[0]
            .GetString()
            .Should().Be("Source and destination accounts must be different.");
        await AssertBalancesUnchanged(accounts, 2000, 300);
    }

    [Test]
    public async Task Post_transfer_with_insufficient_funds_is_rejected_without_writes()
    {
        var accounts = await SeedTransferAccounts(500, 300);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 501, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be("Insufficient funds");
        document.RootElement.GetProperty("errors").TryGetProperty("amount.minorUnits", out _).Should().BeTrue();
        await AssertBalancesUnchanged(accounts, 500, 300);
    }

    [Test]
    public async Task Post_transfer_to_a_missing_account_returns_problem_details_without_writes()
    {
        var accounts = await SeedTransferAccounts(2000, 300);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = "9999999999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString().Should().Be("Resource not found");
        await AssertBalancesUnchanged(accounts, 2000, 300);
    }

    [Test]
    public async Task Post_transfer_returns_a_location_that_serves_the_new_transaction()
    {
        var accounts = await SeedTransferAccounts(2000, 300);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 1234, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber,
            reason = "Shared rent"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().MatchRegex("/api/v1/transactions/[1-9][0-9]*$");

        // The header has to actually resolve -- a Location pointing at a 404 is worse than none.
        var followed = await client.GetAsync(response.Headers.Location);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await followed.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reason").GetString().Should().Be("Shared rent");
        document.RootElement.GetProperty("kind").GetString().Should().Be("transfer");
    }

    [Test]
    public async Task Transfer_rolls_back_entirely_when_persistence_fails()
    {
        // Needs a host whose persistence layer fails, so it builds its own rather than using
        // the fixture's. Replaces the old test that injected a failing ITransferRepository.
        using var failing = new WebApiTestFactory(
            await PostgresFixture.CreateDatabase(),
            new FailOnSaveInterceptor<Transaction>());
        using var failingClient = failing.CreateClient();

        using (var scope = failing.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            db.Accounts.AddRange(
                NewAccount(SourceNumber, "Source", 2000),
                NewAccount(DestinationNumber, "Destination", 300));
            await db.SaveChangesAsync();
        }

        var response = await failingClient.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 1234, currency = "EUR" },
            sourceAccountNumber = SourceNumber,
            destinationAccountNumber = DestinationNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var verification = failing.Services.CreateScope();
        var verifyDb = verification.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await verifyDb.Accounts.AsNoTracking().SingleAsync(a => a.AccountNumber == SourceNumber))
            .BalanceMinorUnits.Should().Be(2000);
        (await verifyDb.Accounts.AsNoTracking().SingleAsync(a => a.AccountNumber == DestinationNumber))
            .BalanceMinorUnits.Should().Be(300);
        (await verifyDb.Transactions.CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------- multi-currency
    //
    // Accounts are denominated. Xpense holds several currencies but never converts between
    // them, so anything that would mix currencies is rejected rather than silently moving the
    // wrong quantity of money -- which is what happened when Balance was a bare decimal.

    [Test]
    public async Task Accounts_can_be_created_in_different_currencies()
    {
        var euro = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"label\":\"Euro\",\"balance\":{\"minorUnits\":1000,\"currency\":\"EUR\"}}"));
        var dollar = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"label\":\"Dollar\",\"balance\":{\"minorUnits\":2500,\"currency\":\"USD\"}}"));

        euro.StatusCode.Should().Be(HttpStatusCode.Created);
        dollar.StatusCode.Should().Be(HttpStatusCode.Created);

        using var euroDoc = JsonDocument.Parse(await euro.Content.ReadAsStringAsync());
        using var dollarDoc = JsonDocument.Parse(await dollar.Content.ReadAsStringAsync());

        euroDoc.RootElement.GetProperty("balance").GetProperty("minorUnits").GetInt64().Should().Be(1000);
        euroDoc.RootElement.GetProperty("balance").GetProperty("currency").GetString().Should().Be("EUR");
        dollarDoc.RootElement.GetProperty("balance").GetProperty("minorUnits").GetInt64().Should().Be(2500);
        dollarDoc.RootElement.GetProperty("balance").GetProperty("currency").GetString().Should().Be("USD");
    }

    [Test]
    public async Task Transaction_in_a_currency_the_account_does_not_hold_is_rejected()
    {
        var seeded = await SeedAccountAndCategory(2000);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 500, currency = "USD" },  // the account is EUR
            sourceAccountNumber = seeded.AccountNumber,
            categoryId = seeded.CategoryId,
            merchant = new { label = "Coffee Shop", create = true }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("detail").GetString()
            .Should().Contain("EUR").And.Contain("USD");

        // Rejected before anything moved.
        (await GetAccountBalance(seeded.AccountNumber)).Should().Be(2000);
    }

    [Test]
    public async Task Transfer_between_accounts_in_different_currencies_is_rejected_without_writes()
    {
        var accounts = await SeedTransferAccounts(2000, 300, Currency.EUR, Currency.USD);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "EUR" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("detail").GetString()
            .Should().Contain("different currencies");

        await AssertBalancesUnchanged(accounts, 2000, 300);
    }

    [Test]
    public async Task Transfer_whose_amount_currency_differs_from_the_accounts_is_rejected()
    {
        var accounts = await SeedTransferAccounts(2000, 300, Currency.EUR, Currency.EUR);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            amount = new { minorUnits = 100, currency = "USD" },
            sourceAccountNumber = accounts.SourceNumber,
            destinationAccountNumber = accounts.DestinationNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertBalancesUnchanged(accounts, 2000, 300);
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
        var totals = summary.GetProperty("totals");
        totals.GetArrayLength().Should().Be(1);
        totals[0].GetProperty("minorUnits").GetInt64().Should().Be(1250);
        totals[0].GetProperty("currency").GetString().Should().Be("EUR");
        var expenses = summary.GetProperty("expenses");
        expenses.GetArrayLength().Should().Be(1);
        expenses[0].GetProperty("category").GetProperty("label").GetString().Should().Be("Food");
        expenses[0].GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(1250);
    }

    /// <summary>
    /// Spending means expenses. Transfers have no category to group by, and income is not spending
    /// -- nothing filtered by direction before, so both would have been counted.
    /// </summary>
    [Test]
    public async Task Get_spending_by_category_counts_neither_income_nor_transfers()
    {
        await SeedTodayExpense(alsoSeedIncomeAndTransfer: true);

        var response = await client.GetAsync("/api/v1/analytics/spending/by-category");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var totals = document.RootElement.GetProperty("totals");
        totals.GetArrayLength().Should().Be(1);
        totals[0].GetProperty("minorUnits").GetInt64().Should().Be(1250);
    }

    /// <summary>
    /// This used to label the whole day with the first expense's currency and add every minor unit
    /// together, so 12.50 EUR and 7.00 USD reported as 19.50 EUR -- money created by addition.
    /// </summary>
    [Test]
    public async Task Get_spending_by_category_never_sums_across_currencies()
    {
        await SeedTodayExpensesInTwoCurrencies();

        var response = await client.GetAsync("/api/v1/analytics/spending/by-category");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var totals = document.RootElement.GetProperty("totals");
        totals.GetArrayLength().Should().Be(2);
        totals.EnumerateArray()
            .Select(total => (total.GetProperty("currency").GetString(), total.GetProperty("minorUnits").GetInt64()))
            .Should().BeEquivalentTo([("EUR", 1250L), ("USD", 700L)]);

        // One category, two currencies, so two lines -- never one line holding a nonsense sum.
        var expenses = document.RootElement.GetProperty("expenses");
        expenses.GetArrayLength().Should().Be(2);
        expenses.EnumerateArray()
            .Select(expense => expense.GetProperty("amount").GetProperty("minorUnits").GetInt64())
            .Should().NotContain(1950);
    }

    // ---------------------------------------------------------------- priorities

    /// <summary>
    /// Also proves the SeedPriorities migration ran: these five rows come from the schema, not from
    /// anything this test wrote.
    /// </summary>
    [Test]
    public async Task Get_priorities_returns_the_reference_data_seeded_by_the_migration()
    {
        var response = await client.GetAsync("/api/v1/priorities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetArrayLength().Should().Be(5);
        document.RootElement.EnumerateArray()
            .Select(priority => priority.GetProperty("label").GetString())
            .Should().Equal("Extreme", "High", "Medium", "Low", "None");
    }

    // ---------------------------------------------------------------- budgets

    [Test]
    public async Task Post_budgets_returns_the_created_resource_at_its_id_route()
    {
        var categoryId = await SeedCategoryReturningId();

        var response = await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 30000, "Monthly"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/budgets/1"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("recurrence").GetString().Should().Be("Monthly");
        document.RootElement.GetProperty("amount").GetProperty("minorUnits").GetInt64().Should().Be(30000);
        document.RootElement.GetProperty("category").GetProperty("label").GetString().Should().Be("Food");
        document.RootElement.GetProperty("startsOn").GetString().Should().Be(FirstOfThisMonth());
    }

    /// <summary>
    /// A budget that does not repeat has exactly one window, so it has to say where that window ends.
    /// Guarded by the validator here and by Budget.For underneath it.
    /// </summary>
    [Test]
    public async Task Post_budgets_rejects_a_one_off_with_no_end()
    {
        var categoryId = await SeedCategoryReturningId();

        var response = await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 30000, "None"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    [Test]
    public async Task Get_budgets_reports_spent_and_remaining_for_the_period_holding_today()
    {
        await SeedBudgetWithTodaysExpense(limitMinorUnits: 30000, spentMinorUnits: 1250);

        var response = await client.GetAsync("/api/v1/budgets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var period = document.RootElement[0].GetProperty("period");
        period.GetProperty("name").GetString().Should().Be($"{DateTime.UtcNow:yyyy-MM}");
        period.GetProperty("spent").GetProperty("minorUnits").GetInt64().Should().Be(1250);
        period.GetProperty("remaining").GetProperty("minorUnits").GetInt64().Should().Be(28750);
        period.GetProperty("exceeded").GetBoolean().Should().BeFalse();
        period.GetProperty("uncounted").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task Get_budgets_by_id_marks_a_budget_exceeded_and_reports_a_negative_remaining()
    {
        await SeedBudgetWithTodaysExpense(limitMinorUnits: 1000, spentMinorUnits: 1250);

        var response = await client.GetAsync("/api/v1/budgets/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var period = document.RootElement.GetProperty("period");
        period.GetProperty("spent").GetProperty("minorUnits").GetInt64().Should().Be(1250);
        period.GetProperty("remaining").GetProperty("minorUnits").GetInt64().Should().Be(-250);
        period.GetProperty("exceeded").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A budget counts one currency. Spending the same category in another is reported as uncounted
    /// rather than converted or, worse, added in.
    /// </summary>
    [Test]
    public async Task Get_budgets_reports_spending_in_other_currencies_as_uncounted()
    {
        await SeedBudgetWithTodaysExpense(limitMinorUnits: 30000, spentMinorUnits: 1250, alsoSpendUsd: 700);

        var response = await client.GetAsync("/api/v1/budgets/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var period = document.RootElement.GetProperty("period");
        period.GetProperty("spent").GetProperty("minorUnits").GetInt64().Should().Be(1250);
        var uncounted = period.GetProperty("uncounted");
        uncounted.GetArrayLength().Should().Be(1);
        uncounted[0].GetProperty("currency").GetString().Should().Be("USD");
        uncounted[0].GetProperty("minorUnits").GetInt64().Should().Be(700);
    }

    /// <summary>
    /// Spending means expenses. A transfer has no category to count against, and income is not
    /// spending -- the same rule the analytics slice follows.
    /// </summary>
    [Test]
    public async Task Get_budgets_counts_neither_income_nor_transfers()
    {
        await SeedBudgetWithTodaysExpense(
            limitMinorUnits: 30000, spentMinorUnits: 1250, alsoSeedIncomeAndTransfer: true);

        var response = await client.GetAsync("/api/v1/budgets/1");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("period").GetProperty("spent")
            .GetProperty("minorUnits").GetInt64().Should().Be(1250);
    }

    /// <summary>
    /// Two budgets on one category are two intentions, and Xpense reports both without arbitrating.
    /// See docs/adr/0007-budgets-are-independent-of-one-another.md.
    /// </summary>
    [Test]
    public async Task Get_budgets_reports_every_budget_on_a_category_without_choosing_between_them()
    {
        var categoryId = await SeedCategoryReturningId();
        await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 30000, "Monthly"));
        await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 10000, "Weekly"));

        var response = await client.GetAsync("/api/v1/budgets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetArrayLength().Should().Be(2);
        document.RootElement.EnumerateArray()
            .Select(budget => budget.GetProperty("period").GetProperty("name").GetString())
            .Should().BeEquivalentTo([$"{DateTime.UtcNow:yyyy-MM}", IsoWeekName(DateTime.UtcNow)]);
    }

    [Test]
    public async Task Get_budgets_reports_no_period_for_a_one_off_whose_window_has_passed()
    {
        var categoryId = await SeedCategoryReturningId();

        await client.PostAsync("/api/v1/budgets", JsonBody(
            $"{{\"categoryId\":{categoryId},\"amount\":{{\"minorUnits\":20000,\"currency\":\"EUR\"}},"
            + "\"recurrence\":\"None\",\"startsOn\":\"2020-01-01\",\"endsOn\":\"2020-01-31\"}"));

        var response = await client.GetAsync("/api/v1/budgets");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement[0].GetProperty("period").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Test]
    public async Task Delete_budgets_hides_it_from_the_collection()
    {
        var categoryId = await SeedCategoryReturningId();
        await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 30000, "Monthly"));

        var deleted = await client.DeleteAsync("/api/v1/budgets/1");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var document = JsonDocument.Parse(await (await client.GetAsync("/api/v1/budgets")).Content.ReadAsStringAsync());
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A budget on a category nobody can see measures nothing anyone can ask about. Without this,
    /// the global query filter hides the category and budget reads meet a null one.
    /// </summary>
    [Test]
    public async Task Delete_categories_also_deletes_the_budgets_pointing_at_it()
    {
        var categoryId = await SeedCategoryReturningId();
        await client.PostAsync("/api/v1/budgets", NewBudgetBody(categoryId, 30000, "Monthly"));

        var deleted = await client.DeleteAsync($"/api/v1/categories/{categoryId}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var response = await client.GetAsync("/api/v1/budgets");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task Get_budgets_by_id_returns_a_problem_document_for_an_unknown_id()
    {
        var response = await client.GetAsync("/api/v1/budgets/404");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
    }

    // ---------------------------------------------------------------- error contract
    //
    // Before the exception handlers landed the API produced four different error shapes: two
    // envelope variants (one camelCase, one PascalCase), bare 404s with no body, and real
    // problem details. Nothing caught it because tests only asserted status codes.

    [Test]
    public async Task Unknown_account_returns_problem_details_not_an_envelope()
    {
        var response = await client.GetAsync("/api/v1/accounts/4242424242");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("title").GetString().Should().Be("Resource not found");
        root.GetProperty("detail").GetString().Should().Contain("4242424242");

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
    public async Task Blank_account_label_is_reported_as_a_camel_case_field_error()
    {
        var response = await client.PostAsync(
            "/api/v1/accounts",
            JsonBody("{\"label\":\"\",\"balance\":{\"minorUnits\":1000,\"currency\":\"EUR\"}}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement
            .GetProperty("errors")
            .GetProperty("label")[0]
            .GetString()
            .Should().Be("The label is required.");
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

    /// <summary>
    /// The account number addresses an account; the database key does not. "1" matches the route
    /// pattern but is nobody's account number, so it resolves to a 404 rather than to account 1.
    /// </summary>
    [TestCase("/api/account")]
    [TestCase("/api/account/1000000000")]
    [TestCase("/api/v1/accounts/1")]
    public async Task Legacy_account_put_routes_are_not_available(string route)
    {
        await SeedAccount();

        var response = await client.PutAsync(route, JsonBody("{\"label\":\"Cash\",\"isDefault\":true}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestCase("/api/transaction/deposit")]
    [TestCase("/api/transaction/withdraw")]
    [TestCase("/api/transaction/transfer")]
    [TestCase("/api/v1/transfers")]
    public async Task Removed_transaction_and_transfer_routes_return_not_found(string path)
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

    /// <summary>
    /// Starts on the first of the current month so the budget is measuring today whatever day the
    /// suite runs. A "None" recurrence deliberately gets no endsOn, which is what makes it invalid.
    /// </summary>
    private static StringContent NewBudgetBody(int categoryId, long minorUnits, string recurrence) =>
        JsonBody(
            $"{{\"categoryId\":{categoryId},\"amount\":{{\"minorUnits\":{minorUnits},\"currency\":\"EUR\"}},"
            + $"\"recurrence\":\"{recurrence}\",\"startsOn\":\"{FirstOfThisMonth()}\"}}");

    private static string FirstOfThisMonth()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// The ISO week year is not always the calendar year -- 2027-01-01 sits in week 53 of 2026 -- so
    /// the expected name is built the same way the domain builds it.
    /// </summary>
    private static string IsoWeekName(DateTime instant) =>
        $"{System.Globalization.ISOWeek.GetYear(instant):0000}-W{System.Globalization.ISOWeek.GetWeekOfYear(instant):00}";

    private XpenseDbContext NewDbContext(out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
    }

    private static Account NewAccount(
        string number,
        string label,
        long balanceMinorUnits,
        bool isDefault = false,
        Currency currency = Currency.EUR) => new()
    {
        AccountNumber = number,
        Label = label,
        BalanceMinorUnits = balanceMinorUnits,
        Currency = currency,
        CreatedAt = DateTime.UtcNow,
        IsDefault = isDefault
    };

    private async Task SeedAccount()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            dbContext.Accounts.Add(NewAccount(SourceNumber, "Cash", 0, isDefault: true));
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedPriority()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            dbContext.Priorities.Add(new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedCategory()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            dbContext.Priorities.Add(priority);
            dbContext.Categories.Add(new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow });
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
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            dbContext.Priorities.Add(priority);
            dbContext.Accounts.Add(NewAccount(SourceNumber, "Cash", 0, isDefault: true));
            dbContext.Categories.Add(new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow });
            dbContext.Tags.Add(new Tag { Label = "Travel", CreatedAt = DateTime.UtcNow });
            dbContext.Merchants.Add(new Merchant { Label = "Albert Heijn", CreatedAt = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<SeededAccountAndCategory> SeedAccountAndCategory(long balanceMinorUnits)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            var account = NewAccount(SourceNumber, "Cash", balanceMinorUnits, isDefault: true);
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow };
            dbContext.AddRange(priority, account, category);
            await dbContext.SaveChangesAsync();

            return new SeededAccountAndCategory(account.AccountNumber, category.Id);
        }
    }

    private async Task<SeededTransactions> SeedTransactions()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            var account = NewAccount(SourceNumber, "Cash", 0, isDefault: true);
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow };
            var merchant = new Merchant { Label = "Grocer", CreatedAt = DateTime.UtcNow };
            dbContext.AddRange(priority, account, category, merchant);
            await dbContext.SaveChangesAsync();

            var oldest = NewExpense(500, At(10), account, category, merchant);
            var middle = NewExpense(999, At(11), account, category, merchant);
            var latest = NewIncome(1234, At(12), account, category, merchant);
            dbContext.Transactions.AddRange(oldest, middle, latest);
            await dbContext.SaveChangesAsync();

            return new SeededTransactions(latest.Id, oldest.Id, category.Id, merchant.Id);
        }

        static DateTimeOffset At(int hour) => new(2026, 7, 26, hour, 0, 0, TimeSpan.Zero);
    }

    private async Task<int> SeedCategoryReturningId()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow };
            dbContext.AddRange(priority, category);
            await dbContext.SaveChangesAsync();
            return category.Id;
        }
    }

    /// <summary>
    /// One monthly EUR budget on Food, plus today's spending against it. Optionally spends the same
    /// category in USD, and optionally adds income and a transfer that must not be counted.
    /// </summary>
    private async Task SeedBudgetWithTodaysExpense(
        long limitMinorUnits,
        long spentMinorUnits,
        long alsoSpendUsd = 0,
        bool alsoSeedIncomeAndTransfer = false)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var now = DateTime.UtcNow;
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = now };
            var account = NewAccount(SourceNumber, "Cash", 0, isDefault: true);
            var other = NewAccount(DestinationNumber, "Savings", 0);
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = now };
            var merchant = new Merchant { Label = "Grocer", CreatedAt = now };
            dbContext.AddRange(priority, account, other, category, merchant);
            await dbContext.SaveChangesAsync();

            dbContext.Budgets.Add(Budget.For(
                category,
                Money.OfMinorUnits(limitMinorUnits, Currency.EUR),
                Recurrence.Monthly,
                new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                endsOn: null));

            dbContext.Transactions.Add(NewExpense(spentMinorUnits, now, account, category, merchant));

            if (alsoSpendUsd > 0)
                dbContext.Transactions.Add(
                    NewExpense(alsoSpendUsd, now, account, category, merchant, Currency.USD));

            if (alsoSeedIncomeAndTransfer)
            {
                dbContext.Transactions.Add(NewIncome(9999, now, account, category, merchant));
                dbContext.Transactions.Add(new Transaction
                {
                    AmountMinorUnits = 5555,
                    Currency = Currency.EUR,
                    SourceAccount = account,
                    DestinationAccount = other,
                    Tags = [],
                    OccurredAt = now,
                    CreatedAt = now
                });
            }

            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>Same category, same day, two currencies -- the case that used to be added together.</summary>
    private async Task SeedTodayExpensesInTwoCurrencies()
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var now = DateTime.UtcNow;
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = now };
            var account = NewAccount(SourceNumber, "Cash", 0, isDefault: true);
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = now };
            var merchant = new Merchant { Label = "Grocer", CreatedAt = now };
            dbContext.AddRange(priority, account, category, merchant);
            await dbContext.SaveChangesAsync();

            dbContext.Transactions.Add(NewExpense(1250, now, account, category, merchant));
            dbContext.Transactions.Add(NewExpense(700, now, account, category, merchant, Currency.USD));
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedTodayExpense(bool alsoSeedIncomeAndTransfer = false)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var priority = new Priority { Label = "Normal", Weight = 1, CreatedAt = DateTime.UtcNow };
            var account = NewAccount(SourceNumber, "Cash", 0, isDefault: true);
            var other = NewAccount(DestinationNumber, "Savings", 0);
            var category = new Category { Label = "Food", Priority = priority, CreatedAt = DateTime.UtcNow };
            var merchant = new Merchant { Label = "Grocer", CreatedAt = DateTime.UtcNow };
            dbContext.AddRange(priority, account, other, category, merchant);
            await dbContext.SaveChangesAsync();

            var now = DateTime.UtcNow;
            dbContext.Transactions.Add(NewExpense(1250, now, account, category, merchant));

            if (alsoSeedIncomeAndTransfer)
            {
                dbContext.Transactions.Add(NewIncome(9999, now, account, category, merchant));
                dbContext.Transactions.Add(new Transaction
                {
                    AmountMinorUnits = 5555,
                    Currency = Currency.EUR,
                    SourceAccount = account,
                    DestinationAccount = other,
                    Tags = [],
                    OccurredAt = now,
                    CreatedAt = now
                });
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task<TransferAccounts> SeedTransferAccounts(
        long sourceBalanceMinorUnits,
        long destinationBalanceMinorUnits,
        Currency sourceCurrency = Currency.EUR,
        Currency destinationCurrency = Currency.EUR)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            var source = NewAccount(SourceNumber, "Source", sourceBalanceMinorUnits, currency: sourceCurrency);
            var destination = NewAccount(
                DestinationNumber, "Destination", destinationBalanceMinorUnits, currency: destinationCurrency);
            dbContext.Accounts.AddRange(source, destination);
            await dbContext.SaveChangesAsync();
            return new TransferAccounts(source.AccountNumber, destination.AccountNumber);
        }
    }

    private static Transaction NewExpense(
        long amountMinorUnits,
        DateTimeOffset occurredAt,
        Account source,
        Category category,
        Merchant merchant,
        Currency currency = Currency.EUR) => new()
    {
        AmountMinorUnits = amountMinorUnits,
        Currency = currency,
        SourceAccount = source,
        Category = category,
        Merchant = merchant,
        Tags = [],
        OccurredAt = occurredAt.UtcDateTime,
        CreatedAt = DateTime.UtcNow
    };

    private static Transaction NewIncome(
        long amountMinorUnits,
        DateTimeOffset occurredAt,
        Account destination,
        Category category,
        Merchant merchant) => new()
    {
        AmountMinorUnits = amountMinorUnits,
        Currency = Currency.EUR,
        DestinationAccount = destination,
        Category = category,
        Merchant = merchant,
        Tags = [],
        OccurredAt = occurredAt.UtcDateTime,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<long> GetAccountBalance(string accountNumber)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            return await dbContext.Accounts.AsNoTracking()
                .Where(account => account.AccountNumber == accountNumber)
                .Select(account => account.BalanceMinorUnits)
                .SingleAsync();
        }
    }

    private async Task AssertBalancesUnchanged(
        TransferAccounts accounts,
        long sourceBalanceMinorUnits,
        long destinationBalanceMinorUnits)
    {
        var dbContext = NewDbContext(out var scope);
        using (scope)
        {
            (await dbContext.Accounts.AsNoTracking()
                    .SingleAsync(account => account.AccountNumber == accounts.SourceNumber))
                .BalanceMinorUnits.Should().Be(sourceBalanceMinorUnits);
            (await dbContext.Accounts.AsNoTracking()
                    .SingleAsync(account => account.AccountNumber == accounts.DestinationNumber))
                .BalanceMinorUnits.Should().Be(destinationBalanceMinorUnits);
            (await dbContext.Transactions.CountAsync()).Should().Be(0);
        }
    }

    private sealed record SeededAccountAndCategory(string AccountNumber, int CategoryId);

    private sealed record SeededTransactions(
        int LatestTransactionId,
        int OldestTransactionId,
        int CategoryId,
        int MerchantId);

    private sealed record TransferAccounts(string SourceNumber, string DestinationNumber);
}
