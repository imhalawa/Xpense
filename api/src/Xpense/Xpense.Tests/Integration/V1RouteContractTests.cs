using System.Net;
using FluentAssertions;
using NUnit.Framework;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class V1RouteContractTests
{
    [Test]
    public async Task GetAccounts_uses_the_v1_plural_resource_route()
    {
        using var factory = new WebApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
