using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class OpenApiSecurityContractTests
{
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp() => connectionString = await PostgresFixture.CreateDatabase();

    [Test]
    public void Generated_document_defines_the_exact_cookie_and_antiforgery_schemes()
    {
        using var document = Generate();
        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");

        Scheme(schemes, "sessionCookie").Should().Be(("apiKey", "cookie", "xpense.session"));
        Scheme(schemes, "antiforgeryHeader").Should().Be(("apiKey", "header", "X-Xpense-Antiforgery"));
        Scheme(schemes, "antiforgeryCookie").Should().Be(("apiKey", "cookie", "xpense.antiforgery"));
    }

    [Test]
    public void Generated_document_marks_anonymous_routes_with_no_security()
    {
        using var document = Generate();

        SecurityNames(Operation(document, "/api/v1/auth/antiforgery", "get")).Should().BeEmpty();
        SecurityNames(Operation(document, "/api/v1/auth/register", "post")).Should().BeEmpty();
        SecurityNames(Operation(document, "/api/v1/invitations/{token}", "get")).Should().BeEmpty();
    }

    [Test]
    public void Generated_document_marks_protected_reads_with_the_session_cookie_only()
    {
        using var document = Generate();

        SecurityNames(Operation(document, "/api/v1/groups", "get"))
            .Should().Equal("sessionCookie");
        SecurityNames(Operation(document, "/api/v1/auth/me", "get"))
            .Should().Equal("sessionCookie");
    }

    [Test]
    public void Generated_document_marks_protected_mutations_with_all_three_requirements()
    {
        using var document = Generate();

        SecurityNames(Operation(document, "/api/v1/groups", "post"))
            .Should().BeEquivalentTo("sessionCookie", "antiforgeryHeader", "antiforgeryCookie");
        SecurityNames(Operation(document, "/api/v1/auth/logout", "post"))
            .Should().BeEquivalentTo("sessionCookie", "antiforgeryHeader", "antiforgeryCookie");
    }

    [Test]
    public void Generated_document_keeps_response_and_request_schemas_and_excludes_test_routes()
    {
        using var document = Generate();
        var paths = document.RootElement.GetProperty("paths");
        var create = Operation(document, "/api/v1/groups", "post");
        var list = Operation(document, "/api/v1/groups", "get");

        create.GetProperty("responses").TryGetProperty("201", out _).Should().BeTrue();
        create.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").ValueKind.Should().Be(JsonValueKind.Object);
        list.GetProperty("responses").TryGetProperty("200", out _).Should().BeTrue();
        paths.EnumerateObject().Select(path => path.Name)
            .Should().NotContain(path => path.StartsWith("/test/", StringComparison.Ordinal));
    }

    private JsonDocument Generate()
    {
        using var factory = new WebApiTestFactory(connectionString);
        var swagger = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var output = new StringWriter();
        var writer = new OpenApiJsonWriter(output);
        swagger.SerializeAsV3(writer);
        return JsonDocument.Parse(output.ToString());
    }

    private static (string Type, string In, string Name) Scheme(JsonElement schemes, string name)
    {
        var scheme = schemes.GetProperty(name);
        return (
            scheme.GetProperty("type").GetString()!,
            scheme.GetProperty("in").GetString()!,
            scheme.GetProperty("name").GetString()!);
    }

    private static JsonElement Operation(JsonDocument document, string path, string method) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static string[] SecurityNames(JsonElement operation)
    {
        if (!operation.TryGetProperty("security", out var security) || security.GetArrayLength() == 0)
            return [];

        return security[0].EnumerateObject().Select(requirement => requirement.Name).ToArray();
    }
}
