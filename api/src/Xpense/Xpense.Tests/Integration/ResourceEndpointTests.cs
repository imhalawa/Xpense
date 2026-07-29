using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Xpense.Persistence;
using Xpense.Services.Entities;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class ResourceEndpointTests
{
    [TestCase("/api/v1/accounts")]
    [TestCase("/api/v1/categories")]
    [TestCase("/api/v1/tags")]
    [TestCase("/api/v1/merchants")]
    public async Task Get_resource_collections_uses_plural_v1_routes_and_returns_resources_directly(string route)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        await SeedAccount(factory);

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        if (route == "/api/v1/accounts")
            document.RootElement[0].GetProperty("label").GetString().Should().Be("Cash");
    }

    [TestCase("/api/category")]
    [TestCase("/api/tag")]
    [TestCase("/api/merchant")]
    public async Task Legacy_singular_resource_routes_are_not_available(string route)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Post_accounts_returns_created_resource_at_its_id_route()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedAccount(factory);

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
    public async Task Post_categories_returns_created_resource_at_its_id_route()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedPriority(factory);

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
    public async Task Post_tags_returns_created_resource_at_its_id_route()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/tags",
            JsonBody("{\"label\":\"Travel\",\"bgColorHex\":\"#ffffff\",\"fgColorHex\":\"#000000\"}"));

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
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsync(
            "/api/v1/tags",
            JsonBody("{\"label\":\"Travel\",\"bgColorHex\":\"#ffffff\",\"fgColorHex\":\"#000000\"}"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().Be(new Uri("http://localhost/api/v1/tags/1"));

        var response = await client.DeleteAsync(createResponse.Headers.Location);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Get_accounts_by_id_returns_the_direct_resource()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedAccount(factory);

        var response = await client.GetAsync("/api/v1/accounts/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Cash");
    }

    [Test]
    public async Task Put_accounts_updates_the_resource_id_and_delete_returns_no_content()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedAccount(factory);

        var updateResponse = await client.PutAsync(
            "/api/v1/accounts/1",
            JsonBody("{\"name\":\"Updated Cash\",\"isDefault\":false}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/accounts/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Updated Cash");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Get_categories_by_id_returns_the_direct_resource()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedCategory(factory);

        var response = await client.GetAsync("/api/v1/categories/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("label").GetString().Should().Be("Food");
    }

    [Test]
    public async Task Put_categories_updates_the_resource_id_and_delete_returns_no_content()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        await SeedCategory(factory);

        var updateResponse = await client.PutAsync(
            "/api/v1/categories/1",
            JsonBody("{\"name\":\"Dining\",\"priorityId\":1}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/categories/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Dining");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Put_tags_updates_the_resource_id_and_delete_returns_no_content()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsync(
            "/api/v1/tags",
            JsonBody("{\"label\":\"Travel\",\"bgColorHex\":\"#ffffff\",\"fgColorHex\":\"#000000\"}"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var updateResponse = await client.PutAsync(
            "/api/v1/tags/1",
            JsonBody("{\"name\":\"Holiday\",\"bgColorHex\":\"#123456\",\"fgColorHex\":\"#abcdef\"}"));
        var deleteResponse = await client.DeleteAsync("/api/v1/tags/1");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("label").GetString().Should().Be("Holiday");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [TestCase("/api/account")]
    [TestCase("/api/account/1000000000")]
    [TestCase("/api/v1/accounts/1000000000")]
    public async Task Legacy_account_number_item_and_collection_put_routes_are_not_available(string route)
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsync(route, JsonBody("{\"name\":\"Cash\",\"isDefault\":true}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task SeedAccount(WebApiTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Accounts.Add(new Account
        {
            Name = "Cash",
            AccountNumber = "1000000000",
            Balance = 0,
            CreatedOn = DateTime.UtcNow,
            IsDefaultAccount = true
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPriority(WebApiTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Priorities.Add(new Priority
        {
            Label = "Normal",
            Weight = 1,
            CreatedOn = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCategory(WebApiTestFactory factory)
    {
        await SeedPriority(factory);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Categories.Add(new Category
        {
            Label = "Food",
            PriorityId = 1,
            CreatedOn = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}
