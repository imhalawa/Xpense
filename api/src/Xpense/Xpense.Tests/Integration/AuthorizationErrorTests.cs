using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
public class AuthorizationErrorTests
{
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp() => connectionString = await PostgresFixture.CreateDatabase();

    private static IEnumerable<TestCaseData> ConflictCases()
    {
        yield return new TestCaseData(
            "invitation-conflict",
            "Invitation state conflict",
            "This invitation has already been used by this account.",
            "InvitationStateConflict");
        yield return new TestCaseData(
            "last-wrapper",
            "Vault recovery method required",
            "The final vault recovery method cannot be removed",
            "LastVaultWrapper");
        yield return new TestCaseData(
            "grant-duplicate",
            "Resource grant already active",
            "This group already has an active grant for the resource.",
            "ResourceGrantAlreadyActive");
    }

    [TestCaseSource(nameof(ConflictCases))]
    public async Task Conflicts_pass_through_the_real_pipeline_with_exact_RFC7807_contracts(
        string kind,
        string title,
        string detail,
        string errorCode)
    {
        using var factory = new WebApiTestFactory(connectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/test/errors/{kind}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        body.RootElement.GetProperty("title").GetString().Should().Be(title);
        body.RootElement.GetProperty("detail").GetString().Should().Be(detail);
        body.RootElement.GetProperty("errorCode").GetString().Should().Be(errorCode);
    }

    [Test]
    public async Task Neutral_invitation_failures_are_byte_identical_and_omit_instance()
    {
        using var factory = new WebApiTestFactory(connectionString);
        using var client = factory.CreateClient();

        var first = await client.GetAsync("/test/errors/invitation-invalid?case=missing");
        var second = await client.GetAsync("/test/errors/invitation-invalid?case=revoked");

        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        first.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var firstBytes = await first.Content.ReadAsByteArrayAsync();
        var secondBytes = await second.Content.ReadAsByteArrayAsync();
        firstBytes.Should().Equal(secondBytes);
        JsonDocument.Parse(firstBytes).RootElement.TryGetProperty("instance", out _).Should().BeFalse();
    }

    [TestCase("validation", HttpStatusCode.BadRequest, "One or more validation errors occurred.")]
    [TestCase("domain", HttpStatusCode.BadRequest, "Request breaks a domain rule")]
    [TestCase("persistence", HttpStatusCode.InternalServerError, "The change could not be saved")]
    [TestCase("fallback", HttpStatusCode.InternalServerError, "Internal Server Error")]
    public async Task Representative_errors_use_problem_details_media(
        string kind,
        HttpStatusCode status,
        string title)
    {
        using var factory = new WebApiTestFactory(connectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/test/errors/{kind}");

        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("title").GetString().Should().Be(title);
    }

    [Test]
    public async Task Cookie_challenge_is_an_empty_401_without_redirect_or_problem_details()
    {
        using var factory = new WebApiTestFactory(connectionString);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/v1/groups");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        response.Content.Headers.ContentType.Should().BeNull();
    }

    [Test]
    public async Task Antiforgery_rejection_is_an_intentionally_empty_400_not_problem_details()
    {
        using var factory = new WebApiTestFactory(connectionString);
        using var client = await factory.CreateAuthenticatedClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/test/authentication/antiforgery-protected");
        request.Headers.Add("X-Xpense-Antiforgery", "wrong");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        response.Content.Headers.ContentType.Should().BeNull();
    }

    [Test]
    public async Task Authenticated_private_group_reads_are_neutral_404_never_403()
    {
        var callerId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var unauthorizedGroupId = Guid.CreateVersion7();
        var otherGroupId = Guid.CreateVersion7();
        var revokedGroupId = Guid.CreateVersion7();
        var deletedGroupId = Guid.CreateVersion7();
        using var factory = new WebApiTestFactory(connectionString).AsUser(callerId);
        using var client = factory.CreateClient();
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            dbContext.Users.AddRange(User(callerId, "caller@example.test", now), User(ownerId, "owner@example.test", now));
            dbContext.Groups.AddRange(
                Group(unauthorizedGroupId, ownerId, false, now),
                Group(otherGroupId, ownerId, false, now),
                Group(revokedGroupId, ownerId, false, now),
                Group(deletedGroupId, ownerId, true, now));
            dbContext.GroupMemberships.AddRange(
                Membership(otherGroupId, callerId, MembershipState.Active, now),
                Membership(revokedGroupId, callerId, MembershipState.Revoked, now),
                Membership(deletedGroupId, callerId, MembershipState.Active, now));
            await dbContext.SaveChangesAsync();
        }

        var paths = new[]
        {
            $"/api/v1/groups/{Guid.CreateVersion7()}",
            $"/api/v1/groups/{unauthorizedGroupId}",
            $"/api/v1/groups/{revokedGroupId}",
            $"/api/v1/groups/{deletedGroupId}",
            $"/api/v1/groups/{unauthorizedGroupId}/members"
        };

        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, path);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, path);
        }
    }

    private static XpenseUser User(Guid id, string email, DateTime now) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString("N"),
        ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        State = AccountState.Active,
        CreatedAt = now
    };

    private static Group Group(Guid id, Guid ownerId, bool deleted, DateTime now) => new()
    {
        Id = id,
        OwnerUserId = ownerId,
        NameCiphertext = [1],
        NameNonce = [2],
        ProtocolVersion = 1,
        IsDeleted = deleted,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static GroupMembership Membership(
        Guid groupId,
        Guid userId,
        MembershipState state,
        DateTime now) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        UserId = userId,
        Role = MembershipRole.Member,
        State = state,
        GroupKeyEnvelope = state == MembershipState.Active ? [3] : null,
        EnvelopeProtocolVersion = state == MembershipState.Active ? 1 : 0,
        RevokedAt = state == MembershipState.Revoked ? now : null,
        CreatedAt = now,
        UpdatedAt = now
    };
}
