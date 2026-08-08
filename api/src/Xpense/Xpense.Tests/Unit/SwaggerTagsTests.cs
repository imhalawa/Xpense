using FluentAssertions;
using Xpense.API.Infrastructure;

namespace Xpense.Tests.Unit;

[TestFixture]
public class SwaggerTagsTests
{
    [TestCase("api/v1/accounts", "Accounts")]
    [TestCase("api/v1/accounts/{accountNumber}", "Accounts")]
    [TestCase("api/v1/budgets", "Budgets")]
    [TestCase("api/v1/budgets/{id}", "Budgets")]
    [TestCase("api/v1/categories", "Categories")]
    [TestCase("api/v1/merchants", "Merchants")]
    [TestCase("api/v1/tags", "Tags")]
    [TestCase("api/v1/priorities", "Priorities")]
    [TestCase("api/v1/transactions", "Transactions")]
    [TestCase("api/v1/transactions/{id}", "Transactions")]
    [TestCase("api/v1/analytics/spending/by-category", "Analytics")]
    [TestCase("api/v1/notifications", "Notifications")]
    [TestCase("api/v1/notifications/unread-count", "Notifications")]
    [TestCase("api/v1/notifications/{id}/read", "Notifications")]
    public void Groups_a_route_by_its_resource(string relativePath, string expected)
    {
        SwaggerTags.ForRoute(relativePath).Should().Be(expected);
    }

    [Test]
    public void Groups_every_operation_on_one_resource_under_the_same_tag()
    {
        var list = SwaggerTags.ForRoute("api/v1/accounts");
        var byNumber = SwaggerTags.ForRoute("api/v1/accounts/{accountNumber}");

        byNumber.Should().Be(list);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Falls_back_when_the_route_is_missing(string? relativePath)
    {
        SwaggerTags.ForRoute(relativePath).Should().Be("General");
    }

    [Test]
    public void Keeps_a_route_that_does_not_carry_the_version_prefix()
    {
        SwaggerTags.ForRoute("health").Should().Be("Health");
    }

    [Test]
    public void Skips_a_leading_route_parameter_rather_than_naming_a_group_after_it()
    {
        SwaggerTags.ForRoute("api/v1/{id}/something").Should().Be("Something");
    }
}
