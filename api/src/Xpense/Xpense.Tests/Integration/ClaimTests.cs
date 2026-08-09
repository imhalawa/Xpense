using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class ClaimTests
{
    private string connectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        connectionString = await PostgresFixture.CreateDatabase();
    }

    [Test]
    public async Task Claim_start_is_neutral_when_claim_mode_is_disabled()
    {
        await using var factory = new WebApiTestFactory(connectionString);
        using var client = await factory.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/v1/claim/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadFromJsonAsync<ProblemContract>())!.Title.Should()
            .Be("Legacy claim unavailable");
    }

    [Test]
    public async Task Claim_status_reports_legacy_only_when_claim_mode_is_disabled_and_never_completed()
    {
        await using var factory = new WebApiTestFactory(connectionString);
        using var client = await factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/claim/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ClaimStatusContract>())!.Mode.Should().Be("legacy");
    }

    [Test]
    public async Task Claim_status_reports_claiming_while_claim_mode_is_enabled()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);

        var response = await client.GetAsync("/api/v1/claim/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ClaimStatusContract>())!.Mode.Should().Be("claiming");
    }

    [Test]
    public async Task Claim_status_reports_encrypted_for_a_fresh_post_contract_database()
    {
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyDataMode("encrypted");
        using var client = await factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/claim/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ClaimStatusContract>())!.Mode.Should().Be("encrypted");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Claim_status_remains_encrypted_after_completion_when_claim_mode_is_disabled()
    {
        var userId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString);
        using var client = await factory.CreateAuthenticatedClient(userId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var now = DateTime.UtcNow;
            dbContext.ClaimTokens.Add(new ClaimToken
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                TokenHash = RandomNumberGenerator.GetBytes(32),
                ExpiresAt = now.AddMinutes(30),
                DatasetDownloadedAt = now,
                ExpectedRecordCount = 0,
                ExpectedTypeCounts = "{}",
                ExpectedManifestHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("0")),
                ExpectedSourceContentHash = SHA256.HashData([]),
                ConsumedAt = now,
                CreatedAt = now.AddSeconds(-1),
                UpdatedAt = now
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/claim/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ClaimStatusContract>())!.Mode.Should().Be("encrypted");
    }

    [Test]
    public async Task Claim_start_issues_one_opaque_30_minute_token_for_the_designated_user()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var before = DateTime.UtcNow;

        var response = await client.PostAsync("/api/v1/claim/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contract = await response.Content.ReadFromJsonAsync<StartContract>();
        contract.Should().NotBeNull();
        var tokenBytes = DecodeBase64Url(contract!.ClaimToken);
        tokenBytes.Should().HaveCount(32);
        contract.ExpiresAt.Should().BeCloseTo(before.AddMinutes(30), TimeSpan.FromSeconds(3));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var stored = await dbContext.ClaimTokens.SingleAsync();
        stored.UserId.Should().Be(designatedUserId);
        stored.TokenHash.Should().Equal(SHA256.HashData(tokenBytes));
        stored.ExpiresAt.Should().Be(contract.ExpiresAt);
        stored.ConsumedAt.Should().BeNull();
        (await dbContext.Users.CountAsync()).Should().Be(1);
        System.Text.Json.JsonSerializer.Serialize(stored).Should().NotContain(contract.ClaimToken);
        stored.TokenHash.Should().NotEqual(System.Text.Encoding.UTF8.GetBytes(contract.ClaimToken));
    }

    [Test]
    public async Task Claim_start_returns_the_same_neutral_body_to_the_wrong_user()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var wrongUserClient = await factory.CreateAuthenticatedClient();

        var first = await wrongUserClient.PostAsync("/api/v1/claim/start", null);
        var second = await wrongUserClient.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = "malformed" });

        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await first.Content.ReadAsByteArrayAsync()).Should()
            .Equal(await second.Content.ReadAsByteArrayAsync());
    }

    [Test]
    public void Legacy_record_ids_and_manifest_are_stable_known_answers()
    {
        LegacyClaimDatasetBuilder.Id("account", 42).Should()
            .Be(Guid.Parse("1b101fae-3502-567d-bc78-35aadc1f5658"));
        LegacyClaimDatasetBuilder.BuildManifest([
            ("tag", Guid.Parse("00000000-0000-0000-0000-000000000002")),
            ("account", Guid.Parse("00000000-0000-0000-0000-000000000001"))
        ]).Should().Be(
            "2\naccount|00000000-0000-0000-0000-000000000001\ntag|00000000-0000-0000-0000-000000000002");
        LegacyClaimDatasetBuilder.BuildManifest([]).Should().Be("0");
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("0"))).Should()
            .Be("5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9");
    }

    [Test]
    public async Task Dataset_contains_every_legacy_type_relationship_and_tombstone_once_and_pins_snapshot()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        await SeedLegacyDataset(factory);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = start.ClaimToken });
        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = start.ClaimToken });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResponse.Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>();
        first.Should().BeEquivalentTo(second);
        first!.ProtocolVersion.Should().Be(1);
        first.RecordCount.Should().Be(14);
        first.Counts.Should().HaveCount(9);
        first.Counts["transaction"].Should().Be(0);
        first.Counts["transfer"].Should().Be(1);
        first.Accounts.Should().HaveCount(2);
        first.Accounts.Single(account => account.IsDeleted).Label.Should().Be("Closed");
        first.NecessityScales.Should().HaveCount(6);
        first.NecessityScales.Should().ContainSingle(scale => scale.Label == "Need");
        first.Categories.Should().ContainSingle();
        first.Merchants.Should().ContainSingle();
        first.Tags.Should().ContainSingle();
        first.Transactions.Should().ContainSingle();
        var transfer = first.Transactions.Single();
        transfer.Kind.Should().Be(TransactionKind.Transfer);
        transfer.SourceAccountId.Should().Be(first.Accounts.Single(account => account.Label == "Cash").Id);
        transfer.DestinationAccountId.Should().Be(first.Accounts.Single(account => account.Label == "Closed").Id);
        transfer.TagIds.Should().Equal(first.Tags.Single().Id);
        first.Budgets.Should().ContainSingle();
        first.Notifications.Should().ContainSingle();
        first.ManifestHash.Should().MatchRegex("^[0-9a-f]{64}$");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var stored = await dbContext.ClaimTokens.SingleAsync();
        stored.DatasetDownloadedAt.Should().NotBeNull();
        stored.ExpectedRecordCount.Should().Be(14);
        stored.ExpectedManifestHash.Should().Equal(Convert.FromHexString(first.ManifestHash));
        stored.ExpectedSourceContentHash.Should().HaveCount(32);
        stored.ConsumedAt.Should().BeNull();
    }

    [Test]
    public async Task Dataset_rejects_source_drift_without_consuming_or_modifying_legacy_rows()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        (await client.PostAsJsonAsync("/api/v1/claim/dataset", new { claimToken = start.ClaimToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            dbContext.Accounts.Add(new Account
            {
                Label = "Later",
                AccountNumber = "L-1",
                Currency = Currency.EUR,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = start.ClaimToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ErrorContract>())!.ErrorCode.Should()
            .Be("LegacyClaimVerificationFailed");
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await verifyDbContext.ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
        (await verifyDbContext.Accounts.SingleAsync()).Label.Should().Be("Later");
    }

    [Test]
    public async Task Complete_requires_a_downloaded_exact_personally_enveloped_record_set()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;

        var beforeDownload = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });

        beforeDownload.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset, omitLast: true);
        var incomplete = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });

        incomplete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await incomplete.Content.ReadFromJsonAsync<ErrorContract>())!.ErrorCode.Should()
            .Be("LegacyClaimVerificationFailed");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
    }

    [Test]
    public async Task Complete_consumes_only_after_exact_verification_and_leaves_plaintext_byte_equivalent()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        await SeedLegacyDataset(factory);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);
        var plaintextBefore = await LegacySnapshot(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await response.Content.ReadFromJsonAsync<CompleteContract>();
        completed!.RecordCount.Should().Be(dataset.RecordCount);
        completed.ManifestHash.Should().Be(dataset.ManifestHash);
        completed.CompletedAt.Kind.Should().Be(DateTimeKind.Utc);
        (await LegacySnapshot(factory)).Should().Be(plaintextBefore);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().Be(completed.CompletedAt);
        var replay = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });
        replay.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var restart = await client.PostAsync("/api/v1/claim/start", null);
        restart.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await restart.Content.ReadFromJsonAsync<ErrorContract>())!.ErrorCode.Should()
            .Be("LegacyClaimAlreadyCompleted");
    }

    [Test]
    public async Task Claim_mode_blocks_exactly_the_18_financial_mutations_before_the_handler()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var mutations = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "/api/v1/accounts"),
            (HttpMethod.Put, "/api/v1/accounts/missing"),
            (HttpMethod.Delete, "/api/v1/accounts/missing"),
            (HttpMethod.Post, "/api/v1/transactions"),
            (HttpMethod.Put, "/api/v1/transactions/1"),
            (HttpMethod.Delete, "/api/v1/transactions/1"),
            (HttpMethod.Post, "/api/v1/categories"),
            (HttpMethod.Put, "/api/v1/categories/1"),
            (HttpMethod.Delete, "/api/v1/categories/1"),
            (HttpMethod.Post, "/api/v1/merchants"),
            (HttpMethod.Put, "/api/v1/merchants/1"),
            (HttpMethod.Delete, "/api/v1/merchants/1"),
            (HttpMethod.Post, "/api/v1/tags"),
            (HttpMethod.Put, "/api/v1/tags/1"),
            (HttpMethod.Delete, "/api/v1/tags/1"),
            (HttpMethod.Post, "/api/v1/budgets"),
            (HttpMethod.Put, "/api/v1/budgets/1"),
            (HttpMethod.Delete, "/api/v1/budgets/1")
        };

        foreach (var mutation in mutations)
        {
            using var request = new HttpRequestMessage(mutation.Method!, mutation.Path);
            if (mutation.Method != HttpMethod.Delete)
                request.Content = JsonContent.Create(new { });
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict, mutation.Path);
            (await response.Content.ReadFromJsonAsync<ErrorContract>())!.ErrorCode.Should()
                .Be("LegacyClaimInProgress", mutation.Path);
        }

        (await client.GetAsync("/api/v1/accounts")).StatusCode.Should().Be(HttpStatusCode.OK);
        var sync = await client.PostAsJsonAsync("/api/v1/sync/records", new { records = Array.Empty<object>() });
        sync.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
        var groups = await client.PostAsJsonAsync("/api/v1/groups", new { });
        groups.StatusCode.Should().NotBe(HttpStatusCode.Conflict);
        var markOne = await client.PatchAsync("/api/v1/notifications/1/read", null);
        var markAll = await client.PostAsync("/api/v1/notifications/read-all", null);
        markOne.StatusCode.Should().Be(HttpStatusCode.Conflict);
        markAll.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestCase("missing")]
    [TestCase("extra")]
    [TestCase("owner")]
    [TestCase("type")]
    [TestCase("record-protocol")]
    [TestCase("envelope-protocol")]
    [TestCase("tombstone")]
    [TestCase("parent")]
    [TestCase("root-owner")]
    [TestCase("root-type")]
    [TestCase("payload")]
    [TestCase("record-nonce")]
    [TestCase("wrapped-key")]
    [TestCase("envelope-nonce")]
    public async Task Complete_rejects_every_mismatched_encrypted_invariant_without_touching_plaintext(
        string defect)
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        await SeedLegacyDataset(factory);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);
        if (defect is "owner" or "root-owner")
        {
            using var otherClient = await factory.CreateAuthenticatedClient(Guid.CreateVersion7());
        }
        await CorruptEncryptedDataset(factory, designatedUserId, defect);
        var before = await LegacySnapshot(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ErrorContract>())!.ErrorCode.Should()
            .Be("LegacyClaimVerificationFailed");
        (await LegacySnapshot(factory)).Should().Be(before);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
    }

    [Test]
    public async Task Expired_and_malformed_tokens_have_the_same_neutral_body()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var stored = await dbContext.ClaimTokens.SingleAsync();
            stored.CreatedAt = DateTime.UtcNow.AddHours(-1);
            stored.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        var expired = await client.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = start.ClaimToken });
        var malformed = await client.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = "malformed" });

        expired.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await expired.Content.ReadAsByteArrayAsync()).Should()
            .Equal(await malformed.Content.ReadAsByteArrayAsync());
    }

    [Test]
    public async Task Concurrent_start_rotates_to_one_durable_token_and_invalidates_the_other()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);

        var responses = await Task.WhenAll(
            client.PostAsync("/api/v1/claim/start", null),
            client.PostAsync("/api/v1/claim/start", null));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        var contracts = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<StartContract>()));
        contracts[0]!.ClaimToken.Should().NotBe(contracts[1]!.ClaimToken);
        var downloads = await Task.WhenAll(contracts.Select(contract =>
            client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = contract!.ClaimToken })));
        downloads.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        downloads.Count(response => response.StatusCode == HttpStatusCode.NotFound).Should().Be(1);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Changing_the_designated_user_rotates_the_singleton_token_to_the_new_user()
    {
        var firstUserId = Guid.CreateVersion7();
        await using (var firstFactory = new WebApiTestFactory(connectionString)
                         .WithLegacyClaim(true, firstUserId))
        {
            using var firstClient = await firstFactory.CreateAuthenticatedClient(firstUserId);
            (await firstClient.PostAsync("/api/v1/claim/start", null)).StatusCode.Should()
                .Be(HttpStatusCode.OK);
        }

        var secondUserId = Guid.CreateVersion7();
        await using var secondFactory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, secondUserId);
        using var secondClient = await secondFactory.CreateAuthenticatedClient(secondUserId);
        var start = await secondClient.PostAsync("/api/v1/claim/start", null);
        var contract = await start.Content.ReadFromJsonAsync<StartContract>();

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondClient.PostAsJsonAsync(
            "/api/v1/claim/dataset",
            new { claimToken = contract!.ClaimToken })).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = secondFactory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync();
        stored.UserId.Should().Be(secondUserId);
    }

    [Test]
    public async Task Concurrent_complete_has_one_success_and_one_neutral_loser()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/claim/complete", new { claimToken = start.ClaimToken }),
            client.PostAsJsonAsync("/api/v1/claim/complete", new { claimToken = start.ClaimToken }));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.NotFound).Should().Be(1);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().NotBeNull();
    }

    [Test]
    public void Enabled_claim_mode_requires_a_nonempty_designated_user_uuid_at_startup()
    {
        using var factory = new WebApiTestFactory(connectionString).WithLegacyClaim(true);

        var start = () => factory.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*designated legacy-claim user*");
    }

    [Test]
    public void Encrypted_data_mode_rejects_enabled_claim_mode_at_startup()
    {
        using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyDataMode("encrypted")
            .WithLegacyClaim(true, Guid.CreateVersion7());

        var start = () => factory.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*cannot be enabled after encrypted data mode*");
    }

    [Test]
    public void Unknown_data_mode_is_rejected_at_startup()
    {
        using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyDataMode("future");

        var start = () => factory.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*data mode must be legacy or encrypted*");
    }

    [Test]
    public async Task Claim_token_migration_is_additive_and_byte_preserves_the_legacy_dataset()
    {
        await using var factory = new WebApiTestFactory(connectionString);
        await SeedLegacyDataset(factory);
        var before = await FullLegacySnapshot(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            await dbContext.GetService<IMigrator>()
                .MigrateAsync("20260809043527_AddInvitationEnvelopeProtocolVersion");
        }
        (await FullLegacySnapshot(factory)).Should().Be(before);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            await dbContext.Database.MigrateAsync();
            (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        }

        (await FullLegacySnapshot(factory)).Should().Be(before);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'Xpense' AND table_name = 'ClaimTokens'",
            connection);
        (await command.ExecuteScalarAsync()).Should().Be(1L);
        await using var constraints = new NpgsqlCommand(
            "SELECT conname FROM pg_constraint WHERE conrelid = '\"Xpense\".\"ClaimTokens\"'::regclass",
            connection);
        var names = new List<string>();
        await using var reader = await constraints.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        names.Should().Contain([
            "CK_ClaimToken_Expiry",
            "CK_ClaimToken_Updated",
            "CK_ClaimToken_Consumed"
        ]);
    }

    [Test]
    public async Task Completion_persistence_failure_leaves_token_restartable_and_plaintext_unchanged()
    {
        var interceptor = new ConditionalClaimTokenSaveInterceptor();
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString, interceptor)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);
        var before = await FullLegacySnapshot(factory);
        interceptor.Enabled = true;

        var response = await client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        interceptor.Enabled = false;
        (await FullLegacySnapshot(factory)).Should().Be(before);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
    }

    [Test]
    public async Task Completion_waits_for_a_preexisting_source_writer_and_then_detects_its_commit()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        await SeedLegacyDataset(factory);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);

        await using var writerConnection = new NpgsqlConnection(connectionString);
        await writerConnection.OpenAsync();
        await using var writerTransaction = await writerConnection.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand(
                         "UPDATE \"Xpense\".\"Accounts\" SET \"Label\" = 'Concurrent writer' WHERE \"Label\" = 'Cash'",
                         writerConnection,
                         writerTransaction))
        {
            (await update.ExecuteNonQueryAsync()).Should().Be(1);
        }

        var completion = client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });
        await WaitForWaitingShareLock("Accounts", writerConnection.ProcessID);
        completion.IsCompleted.Should().BeFalse();
        await writerTransaction.CommitAsync();
        var response = await completion;

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        (await dbContext.ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
        (await dbContext.Accounts.SingleAsync(account => account.Label == "Concurrent writer"))
            .Should().NotBeNull();
    }

    [Test]
    public async Task Completion_waits_for_a_preexisting_target_writer_and_then_detects_its_commit()
    {
        var designatedUserId = Guid.CreateVersion7();
        await using var factory = new WebApiTestFactory(connectionString)
            .WithLegacyClaim(true, designatedUserId);
        using var client = await factory.CreateAuthenticatedClient(designatedUserId);
        var start = (await (await client.PostAsync("/api/v1/claim/start", null))
            .Content.ReadFromJsonAsync<StartContract>())!;
        var dataset = (await (await client.PostAsJsonAsync(
                "/api/v1/claim/dataset",
                new { claimToken = start.ClaimToken }))
            .Content.ReadFromJsonAsync<LegacyClaimDatasetResponse>())!;
        await SeedEncryptedDataset(factory, designatedUserId, dataset);

        await using var writerScope = factory.Services.CreateAsyncScope();
        var writerDbContext = writerScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        await using var writerTransaction = await writerDbContext.Database.BeginTransactionAsync();
        var extraId = Guid.CreateVersion7();
        writerDbContext.EncryptedRecords.Add(new EncryptedRecord
        {
            Id = extraId,
            RecordType = EncryptedRecordType.Merchant,
            OwnerUserId = designatedUserId,
            Revision = 1,
            ProtocolVersion = 1,
            Nonce = [1],
            Ciphertext = [2],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        writerDbContext.RecordEnvelopes.Add(new RecordEnvelope
        {
            Id = Guid.CreateVersion7(),
            EncryptedRecordId = extraId,
            WrappedKey = [3],
            Nonce = [4],
            ProtocolVersion = 1
        });
        await writerDbContext.SaveChangesAsync();

        var completion = client.PostAsJsonAsync(
            "/api/v1/claim/complete",
            new { claimToken = start.ClaimToken });
        var writerProcessId = ((NpgsqlConnection)writerDbContext.Database.GetDbConnection()).ProcessID;
        await WaitForWaitingShareLock("EncryptedRecords", writerProcessId);
        completion.IsCompleted.Should().BeFalse();
        await writerTransaction.CommitAsync();
        var response = await completion;

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        (await verifyScope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .ClaimTokens.SingleAsync()).ConsumedAt.Should().BeNull();
    }

    private async Task WaitForWaitingShareLock(string tableName, int writerProcessId)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_locks AS lock
                    JOIN pg_class AS relation ON relation.oid = lock.relation
                    JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
                    WHERE schema.nspname = 'Xpense'
                      AND relation.relname = @tableName
                      AND lock.mode = 'ShareLock'
                      AND NOT lock.granted
                      AND lock.database = (
                          SELECT oid FROM pg_database WHERE datname = current_database())
                      AND @writerProcessId = ANY(pg_blocking_pids(lock.pid)))
                """,
                observer);
            command.Parameters.AddWithValue("tableName", tableName);
            command.Parameters.AddWithValue("writerProcessId", writerProcessId);
            if (await command.ExecuteScalarAsync() is true)
                return;

            await Task.Delay(20);
        }

        Assert.Fail($"Completion did not wait for a ShareLock on Xpense.{tableName} within five seconds.");
    }

    private static async Task CorruptEncryptedDataset(
        WebApiTestFactory factory,
        Guid designatedUserId,
        string defect)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var records = await dbContext.EncryptedRecords.OrderBy(record => record.Id).ToArrayAsync();
        var first = records[0];
        switch (defect)
        {
            case "missing":
                dbContext.EncryptedRecords.Remove(first);
                break;
            case "extra":
                var extra = new EncryptedRecord
                {
                    Id = Guid.CreateVersion7(),
                    RecordType = EncryptedRecordType.Merchant,
                    OwnerUserId = designatedUserId,
                    Revision = 1,
                    ProtocolVersion = 1,
                    Nonce = [1],
                    Ciphertext = [2],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.EncryptedRecords.Add(extra);
                dbContext.RecordEnvelopes.Add(new RecordEnvelope
                {
                    Id = Guid.CreateVersion7(),
                    EncryptedRecordId = extra.Id,
                    WrappedKey = [3],
                    Nonce = [4],
                    ProtocolVersion = 1
                });
                break;
            case "owner":
                first.OwnerUserId = (await dbContext.Users.AsNoTracking()
                    .SingleAsync(user => user.Id != designatedUserId)).Id;
                break;
            case "type":
                first.RecordType = first.RecordType == EncryptedRecordType.Tag
                    ? EncryptedRecordType.Merchant
                    : EncryptedRecordType.Tag;
                break;
            case "record-protocol":
                first.ProtocolVersion = 2;
                break;
            case "envelope-protocol":
                (await dbContext.RecordEnvelopes.SingleAsync(
                    envelope => envelope.EncryptedRecordId == first.Id)).ProtocolVersion = 2;
                break;
            case "tombstone":
                first.IsDeleted = !first.IsDeleted;
                break;
            case "parent":
                var otherParent = records
                    .Where(record => record.ParentResourceId == record.Id && record.Id != first.Id)
                    .Select(record => record.Id)
                    .First();
                first.ParentResourceId = otherParent;
                break;
            case "root-owner":
                (await dbContext.SharedResources.FirstAsync()).OwnerUserId =
                    (await dbContext.Users.AsNoTracking()
                        .SingleAsync(user => user.Id != designatedUserId)).Id;
                break;
            case "root-type":
                var resource = await dbContext.SharedResources.FirstAsync();
                resource.Type = resource.Type == SharedResourceType.Account
                    ? SharedResourceType.Budget
                    : SharedResourceType.Account;
                break;
            case "payload":
                first.Ciphertext = [];
                break;
            case "record-nonce":
                first.Nonce = [];
                break;
            case "wrapped-key":
                (await dbContext.RecordEnvelopes.SingleAsync(
                    envelope => envelope.EncryptedRecordId == first.Id)).WrappedKey = [];
                break;
            case "envelope-nonce":
                (await dbContext.RecordEnvelopes.SingleAsync(
                    envelope => envelope.EncryptedRecordId == first.Id)).Nonce = [];
                break;
            default:
                throw new InvalidOperationException(defect);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedEncryptedDataset(
        WebApiTestFactory factory,
        Guid userId,
        LegacyClaimDatasetResponse dataset,
        bool omitLast = false)
    {
        var expected = new List<(Guid Id, EncryptedRecordType Type, bool IsDeleted, Guid? ParentResourceId)>();
        expected.AddRange(dataset.Accounts.Select(record =>
            (record.Id, EncryptedRecordType.Account, record.IsDeleted, (Guid?)record.Id)));
        expected.AddRange(dataset.NecessityScales.Select(record =>
            (record.Id, EncryptedRecordType.NecessityScale, record.IsDeleted, (Guid?)null)));
        expected.AddRange(dataset.Categories.Select(record =>
            (record.Id, EncryptedRecordType.Category, record.IsDeleted, (Guid?)null)));
        expected.AddRange(dataset.Merchants.Select(record =>
            (record.Id, EncryptedRecordType.Merchant, record.IsDeleted, (Guid?)null)));
        expected.AddRange(dataset.Tags.Select(record =>
            (record.Id, EncryptedRecordType.Tag, record.IsDeleted, (Guid?)null)));
        expected.AddRange(dataset.Transactions.Select(record => (
            record.Id,
            record.Kind == TransactionKind.Transfer ? EncryptedRecordType.Transfer : EncryptedRecordType.Transaction,
            record.IsDeleted,
            record.Kind == TransactionKind.Income ? record.DestinationAccountId : record.SourceAccountId)));
        expected.AddRange(dataset.Budgets.Select(record =>
            (record.Id, EncryptedRecordType.Budget, record.IsDeleted, (Guid?)record.Id)));
        expected.AddRange(dataset.Notifications.Select(record =>
            (record.Id, EncryptedRecordType.Notification, record.IsDeleted, (Guid?)null)));
        if (omitLast)
            expected.RemoveAt(expected.Count - 1);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow;
        foreach (var root in expected.Where(item => item.ParentResourceId == item.Id))
        {
            dbContext.SharedResources.Add(new SharedResource
            {
                Id = root.Id,
                Type = root.Type == EncryptedRecordType.Account
                    ? SharedResourceType.Account
                    : SharedResourceType.Budget,
                OwnerUserId = userId,
                CreatedAt = now
            });
        }

        foreach (var item in expected)
        {
            dbContext.EncryptedRecords.Add(new EncryptedRecord
            {
                Id = item.Id,
                RecordType = item.Type,
                OwnerUserId = userId,
                ParentResourceId = item.ParentResourceId,
                Revision = 1,
                ProtocolVersion = 1,
                Nonce = [1, 2, 3],
                Ciphertext = [4, 5, 6],
                IsDeleted = item.IsDeleted,
                CreatedAt = now,
                UpdatedAt = now
            });
            dbContext.RecordEnvelopes.Add(new RecordEnvelope
            {
                Id = Guid.CreateVersion7(),
                EncryptedRecordId = item.Id,
                WrappedKey = [7, 8, 9],
                Nonce = [10, 11, 12],
                ProtocolVersion = 1
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> LegacySnapshot(WebApiTestFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var accounts = await dbContext.Accounts.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(account => account.Id)
            .Select(account => new { account.Id, account.Label, account.BalanceMinorUnits, account.IsDeleted })
            .ToArrayAsync();
        var transactions = await dbContext.Transactions.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(transaction => transaction.Id)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.AmountMinorUnits,
                transaction.SourceAccountId,
                transaction.DestinationAccountId,
                transaction.IsDeleted
            })
            .ToArrayAsync();
        return System.Text.Json.JsonSerializer.Serialize(new { accounts, transactions });
    }

    private static async Task<string> FullLegacySnapshot(WebApiTestFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var builder = scope.ServiceProvider.GetRequiredService<LegacyClaimDatasetBuilder>();
        var dataset = await builder.Build(CancellationToken.None);
        return System.Text.Json.JsonSerializer.Serialize(dataset.Response);
    }

    private static async Task SeedLegacyDataset(WebApiTestFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var now = DateTime.UtcNow.AddDays(-1);
        var cash = new Account
        {
            Label = "Cash",
            AccountNumber = "A-1",
            BalanceMinorUnits = 1000,
            Currency = Currency.EUR,
            IsDefault = true,
            CreatedAt = now
        };
        var closed = new Account
        {
            Label = "Closed",
            AccountNumber = "A-2",
            BalanceMinorUnits = 200,
            Currency = Currency.EUR,
            IsDeleted = true,
            CreatedAt = now
        };
        var priority = new Priority { Label = "Need", Weight = 0.8, CreatedAt = now };
        var category = new Category { Label = "Food", Priority = priority, CreatedAt = now };
        var merchant = new Merchant { Label = "Market", CreatedAt = now };
        var tag = new Tag
        {
            Label = "shopping",
            BgColorHex = "000000",
            FgColorHex = "ffffff",
            CreatedAt = now
        };
        dbContext.AddRange(cash, closed, priority, category, merchant, tag);
        await dbContext.SaveChangesAsync();
        var transfer = new Transaction
        {
            AmountMinorUnits = 250,
            Currency = Currency.EUR,
            OccurredAt = now.AddHours(1),
            Reason = "Move",
            SourceAccountId = cash.Id,
            DestinationAccountId = closed.Id,
            Tags = [tag],
            CreatedAt = now
        };
        var budget = new Budget
        {
            AmountMinorUnits = 5000,
            Currency = Currency.EUR,
            CategoryId = category.Id,
            Recurrence = Recurrence.Monthly,
            StartsOn = now.Date,
            AlertThresholdPercent = 80,
            CreatedAt = now
        };
        var notification = new Notification
        {
            EventId = Guid.CreateVersion7(),
            Kind = NotificationKind.BudgetExceeded,
            Title = "Budget",
            Message = "Exceeded",
            Payload = "{}",
            PayloadHash = new string('a', 64),
            CreatedAt = now
        };
        dbContext.AddRange(transfer, budget, notification);
        await dbContext.SaveChangesAsync();
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(base64);
    }

    private sealed record StartContract(string ClaimToken, DateTime ExpiresAt);

    private sealed record ClaimStatusContract(string Mode);

    private sealed record ProblemContract(string Title);

    private sealed record ErrorContract(string ErrorCode);

    private sealed record CompleteContract(
        int RecordCount,
        Dictionary<string, int> Counts,
        string ManifestHash,
        DateTime CompletedAt);

    private sealed class ConditionalClaimTokenSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context?.ChangeTracker.Entries<ClaimToken>()
                    .Any(entry => entry.State == EntityState.Modified) == true)
            {
                throw new InvalidOperationException("Simulated claim-token persistence failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
