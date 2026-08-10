using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xpense.API.ExceptionHandlers;
using Xpense.Domain.Exceptions;

namespace Xpense.Tests.Unit;

[TestFixture]
public class StateConflictExceptionHandlerTests
{
    private static IEnumerable<TestCaseData> ConflictCases()
    {
        yield return new TestCaseData(
                new InvitationStateConflictException(),
                "Invitation state conflict",
                "This invitation has already been used by this account.",
                "InvitationStateConflict",
                true)
            .SetName("Invitation_state_conflict_has_its_exact_RFC7807_contract");
        yield return new TestCaseData(
                new LastVaultWrapperException(),
                "Vault recovery method required",
                "The final vault recovery method cannot be removed",
                "LastVaultWrapper",
                true)
            .SetName("Last_vault_wrapper_has_its_exact_RFC7807_contract");
        yield return new TestCaseData(
                new ResourceGrantAlreadyActiveException(),
                "Resource grant already active",
                "This group already has an active grant for the resource.",
                "ResourceGrantAlreadyActive",
                false)
            .SetName("Duplicate_resource_grant_keeps_its_specialized_RFC7807_contract");
    }

    [TestCaseSource(nameof(ConflictCases))]
    public async Task State_conflicts_keep_their_exact_contracts(
        Exception exception,
        string title,
        string detail,
        string errorCode,
        bool consolidated)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddProblemDetails();
        await using var services = serviceCollection.BuildServiceProvider();
        var problemDetailsService = services.GetRequiredService<IProblemDetailsService>();
        IExceptionHandler handler = consolidated
            ? new StateConflictExceptionHandler(problemDetailsService)
            : new ResourceGrantAlreadyActiveExceptionHandler(problemDetailsService);
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/test";
        httpContext.Response.Body = new MemoryStream();

        (await handler.TryHandleAsync(httpContext, exception, default)).Should().BeTrue();

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        httpContext.Response.ContentType.Should().StartWith("application/problem+json");
        httpContext.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(httpContext.Response.Body);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        body.RootElement.GetProperty("title").GetString().Should().Be(title);
        body.RootElement.GetProperty("detail").GetString().Should().Be(detail);
        body.RootElement.GetProperty("errorCode").GetString().Should().Be(errorCode);
        body.RootElement.GetProperty("instance").GetString().Should().Be("POST /api/v1/test");
    }

    [Test]
    public async Task Consolidated_handler_declines_other_domain_rule_violations()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions();
        serviceCollection.AddProblemDetails();
        await using var services = serviceCollection.BuildServiceProvider();
        var handler = new StateConflictExceptionHandler(services.GetRequiredService<IProblemDetailsService>());

        (await handler.TryHandleAsync(
            new DefaultHttpContext { RequestServices = services },
            new GroupOwnerCannotLeaveException(Guid.CreateVersion7()),
            default)).Should().BeFalse();
    }
}
