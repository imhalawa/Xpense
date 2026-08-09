using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class PasskeyManagementTests
{
    private static readonly byte[] FirstCredentialId = [1, 2, 3, 4];
    private static readonly byte[] SecondCredentialId = [9, 8, 7, 6];

    private WebApiTestFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public async Task SetUp()
    {
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase());
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
    }

    [TearDown]
    public async Task TearDown()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Test]
    public async Task Listing_passkeys_returns_labels_and_last_used_times_for_the_current_user_only()
    {
        var secondLastUsedAt = DateTime.UtcNow.AddDays(-2);
        await AddUser("current@example.test", (FirstCredentialId, "Laptop", null), (SecondCredentialId, "Phone", secondLastUsedAt));
        await AddUser("other@example.test", ([21, 22, 23, 24], "Other user", DateTime.UtcNow.AddDays(-3)));
        await SignIn("current@example.test", FirstCredentialId);

        var response = await client.GetAsync("/api/v1/users/me/passkeys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passkeys = (await response.Content.ReadFromJsonAsync<PasskeyResponse[]>())!;
        passkeys.Should().HaveCount(2);
        passkeys.Should().Contain(passkey =>
            passkey.CredentialId == WebEncoders.Base64UrlEncode(FirstCredentialId) &&
            passkey.Label == "Laptop" &&
            passkey.LastUsedAt.HasValue);
        var phone = passkeys.Single(passkey =>
            passkey.CredentialId == WebEncoders.Base64UrlEncode(SecondCredentialId) &&
            passkey.Label == "Phone");
        phone.LastUsedAt.Should().BeCloseTo(secondLastUsedAt, TimeSpan.FromSeconds(1));
        passkeys.Should().NotContain(passkey => passkey.Label == "Other user");
    }

    [Test]
    public async Task Passkey_management_requires_authentication()
    {
        var list = await client.GetAsync("/api/v1/users/me/passkeys");
        var options = await client.PostAsync("/api/v1/users/me/passkeys/options", null);
        var add = await client.PostAsJsonAsync("/api/v1/users/me/passkeys", new { });
        var delete = await client.DeleteAsync("/api/v1/users/me/passkeys/unknown");

        new[] { list, options, add, delete }.Should()
            .OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Passkey_registration_options_are_authoritative_for_five_minutes_and_replace_the_prior_state()
    {
        await AddUser("options@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("options@example.test", FirstCredentialId);

        var first = await CreateManagementOptions(client);
        var second = await CreateManagementOptions(client);

        first.OptionsJson.Should().Contain("challenge");
        second.OptionsJson.Should().Contain("challenge");
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var pending = await dbContext.PendingRegistrations
            .Where(registration => registration.NormalizedEmail == "OPTIONS@EXAMPLE.TEST")
            .ToListAsync();
        pending.Should().HaveCount(2);
        pending.Single(registration => registration.Id == first.PendingRegistrationId).ConsumedAt.Should().NotBeNull();
        var active = pending.Single(registration => registration.Id == second.PendingRegistrationId);
        active.ConsumedAt.Should().BeNull();
        active.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Adding_a_second_passkey_requires_a_wrapper_for_it()
    {
        await AddUser("missing-wrapper@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("missing-wrapper@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);

        var response = await SendMutation(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(
                options.PendingRegistrationId,
                SoftwareAuthenticator.CreateCredential(options.OptionsJson, SecondCredentialId),
                null,
                1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor("MISSING-WRAPPER@EXAMPLE.TEST")).Should().HaveCount(1);
        (await WrappersFor("MISSING-WRAPPER@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().HaveCount(1);
    }

    [Test]
    public async Task Adding_a_second_passkey_creates_its_credential_and_wrapper_atomically()
    {
        await AddUser("add@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("add@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);

        var response = await AddPasskey(client, options, SecondCredentialId, ValidWrapper);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<PasskeyResponse>())!;
        created.CredentialId.Should().Be(WebEncoders.Base64UrlEncode(SecondCredentialId));
        created.Label.Should().Be("Phone");
        (await PasskeysFor("ADD@EXAMPLE.TEST")).Select(passkey => passkey.CredentialId)
            .Should().ContainEquivalentOf(SecondCredentialId);
        var wrapper = (await WrappersFor("ADD@EXAMPLE.TEST", VaultWrapperKind.Passkey))
            .Single(item => item.CredentialId!.SequenceEqual(SecondCredentialId));
        wrapper.Label.Should().Be("Phone");
        wrapper.Salt.Should().Equal([41, 42, 43]);
        wrapper.Ciphertext.Should().Equal([44, 45, 46]);
        wrapper.Nonce.Should().Equal([47, 48, 49]);
    }

    [Test]
    public async Task Replaying_a_passkey_registration_is_refused_without_a_half_record()
    {
        await AddUser("replay@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("replay@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);

        var first = await AddPasskey(client, options, SecondCredentialId, ValidWrapper);
        var replay = await AddPasskey(client, options, [19, 18, 17, 16], ValidWrapper with { Label = "Replay" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor("REPLAY@EXAMPLE.TEST")).Should().HaveCount(2);
        (await WrappersFor("REPLAY@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().HaveCount(2);
    }

    [Test]
    public async Task Invalid_attestation_consumes_the_state_without_creating_a_half_record()
    {
        await AddUser("invalid-add@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("invalid-add@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);

        var response = await SendMutation(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(options.PendingRegistrationId, "invalid", ValidWrapper, 1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor("INVALID-ADD@EXAMPLE.TEST")).Should().HaveCount(1);
        (await WrappersFor("INVALID-ADD@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().HaveCount(1);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>().PendingRegistrations
            .SingleAsync(registration => registration.Id == options.PendingRegistrationId))
            .ConsumedAt.Should().NotBeNull();
    }

    [TestCase("salt")]
    [TestCase("ciphertext")]
    [TestCase("nonce")]
    public async Task Adding_a_passkey_rejects_invalid_wrapper_base64(string field)
    {
        await AddUser($"invalid-{field}@example.test", (FirstCredentialId, "Existing", null));
        await SignIn($"invalid-{field}@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);
        var wrapper = field switch
        {
            "salt" => ValidWrapper with { Salt = "not-base64" },
            "ciphertext" => ValidWrapper with { Ciphertext = "not-base64" },
            _ => ValidWrapper with { Nonce = "not-base64" }
        };

        var response = await AddPasskey(client, options, SecondCredentialId, wrapper);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor($"INVALID-{field.ToUpperInvariant()}@EXAMPLE.TEST")).Should().HaveCount(1);
    }

    [Test]
    public async Task Adding_a_passkey_rejects_an_unknown_protocol_and_an_oversized_label()
    {
        await AddUser("invalid-wrapper@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("invalid-wrapper@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);
        var protocol = await SendMutation(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(
                options.PendingRegistrationId,
                SoftwareAuthenticator.CreateCredential(options.OptionsJson, SecondCredentialId),
                ValidWrapper,
                2));
        var label = await SendMutation(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(
                options.PendingRegistrationId,
                SoftwareAuthenticator.CreateCredential(options.OptionsJson, SecondCredentialId),
                ValidWrapper with { Label = new string('x', 101) },
                1));
        var oversized = await SendMutation(
            client,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(
                options.PendingRegistrationId,
                SoftwareAuthenticator.CreateCredential(options.OptionsJson, SecondCredentialId),
                ValidWrapper with { Salt = Convert.ToBase64String(new byte[4097]) },
                1));

        protocol.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        label.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor("INVALID-WRAPPER@EXAMPLE.TEST")).Should().HaveCount(1);
    }

    [Test]
    public async Task Concurrent_passkey_registration_replay_creates_one_credential_and_wrapper()
    {
        await AddUser("concurrent-add@example.test", (FirstCredentialId, "Existing", null));
        await SignIn("concurrent-add@example.test", FirstCredentialId);
        var options = await CreateManagementOptions(client);

        var responses = await Task.WhenAll(
            AddPasskey(client, options, SecondCredentialId, ValidWrapper),
            AddPasskey(client, options, [19, 18, 17, 16], ValidWrapper with { Label = "Other attempt" }));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
        (await PasskeysFor("CONCURRENT-ADD@EXAMPLE.TEST")).Should().HaveCount(2);
        (await WrappersFor("CONCURRENT-ADD@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().HaveCount(2);
    }

    [Test]
    public async Task Concurrent_users_cannot_claim_the_same_new_passkey_credential()
    {
        var otherInitialCredentialId = new byte[] { 21, 22, 23, 24 };
        await AddUser("collision-first@example.test", (FirstCredentialId, "First existing", null));
        await AddUser("collision-second@example.test", (otherInitialCredentialId, "Second existing", null));
        await SignIn("collision-first@example.test", FirstCredentialId);
        using var otherClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        await SignIn("collision-second@example.test", otherInitialCredentialId, otherClient);
        var firstOptions = await CreateManagementOptions(client);
        var secondOptions = await CreateManagementOptions(otherClient);

        var responses = await Task.WhenAll(
            AddPasskey(client, firstOptions, SecondCredentialId, ValidWrapper with { Label = "First new" }),
            AddPasskey(otherClient, secondOptions, SecondCredentialId, ValidWrapper with { Label = "Second new" }));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var rejected = responses.Single(response => response.StatusCode != HttpStatusCode.Created);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var rejectedBody = await rejected.Content.ReadAsStringAsync();
        rejectedBody.Should().Contain("The passkey registration request is invalid or has expired.");
        rejectedBody.Should().NotContain("collision-first@example.test");
        rejectedBody.Should().NotContain("collision-second@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<XpenseUser>>();
        var pending = await dbContext.PendingRegistrations
            .Where(registration => new[]
            {
                firstOptions.PendingRegistrationId,
                secondOptions.PendingRegistrationId
            }.Contains(registration.Id))
            .ToListAsync();
        pending.Should().HaveCount(2);
        pending.Should().OnlyContain(registration => registration.ConsumedAt.HasValue);
        var owner = await userManager.FindByPasskeyIdAsync(SecondCredentialId);
        owner.Should().NotBeNull();
        var loserNormalizedEmail = owner!.NormalizedEmail == "COLLISION-FIRST@EXAMPLE.TEST"
            ? "COLLISION-SECOND@EXAMPLE.TEST"
            : "COLLISION-FIRST@EXAMPLE.TEST";
        (await userManager.GetPasskeyAsync(owner, SecondCredentialId)).Should().NotBeNull();
        (await WrappersFor(owner.NormalizedEmail!, VaultWrapperKind.Passkey))
            .Should().ContainSingle(wrapper => wrapper.CredentialId!.SequenceEqual(SecondCredentialId));
        (await PasskeysFor(loserNormalizedEmail)).Should().HaveCount(1);
        (await WrappersFor(loserNormalizedEmail, VaultWrapperKind.Passkey))
            .Should().NotContain(wrapper =>
                wrapper.CredentialId != null && wrapper.CredentialId.SequenceEqual(SecondCredentialId));
    }

    [Test]
    public async Task Expired_or_other_user_passkey_registration_state_is_refused()
    {
        await AddUser("state-owner@example.test", (FirstCredentialId, "Owner", null));
        await AddUser("state-other@example.test", ([21, 22, 23, 24], "Other", null));
        await SignIn("state-owner@example.test", FirstCredentialId);
        var expired = await CreateManagementOptions(client);
        await Expire(expired.PendingRegistrationId);
        var expiredResponse = await AddPasskey(client, expired, SecondCredentialId, ValidWrapper);
        var otherUserState = await CreateManagementOptions(client);
        using var otherClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        await SignIn("state-other@example.test", [21, 22, 23, 24], otherClient);

        var otherResponse = await AddPasskey(otherClient, otherUserState, SecondCredentialId, ValidWrapper);

        expiredResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        otherResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PasskeysFor("STATE-OWNER@EXAMPLE.TEST")).Should().HaveCount(1);
        (await PasskeysFor("STATE-OTHER@EXAMPLE.TEST")).Should().HaveCount(1);
    }

    [Test]
    public async Task Deleting_a_passkey_leaves_the_other_one_usable()
    {
        await AddUser("delete@example.test", (FirstCredentialId, "First", null), (SecondCredentialId, "Second", null));
        await SignIn("delete@example.test", FirstCredentialId);

        var response = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode(FirstCredentialId)}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PasskeysFor("DELETE@EXAMPLE.TEST")).Select(passkey => passkey.CredentialId)
            .Should().ContainSingle().Which.Should().Equal(SecondCredentialId);
        (await WrappersFor("DELETE@EXAMPLE.TEST", VaultWrapperKind.Passkey))
            .Should().ContainSingle(wrapper => wrapper.CredentialId!.SequenceEqual(SecondCredentialId));
        using var remainingClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        await SignIn("delete@example.test", SecondCredentialId, remainingClient);
        (await remainingClient.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Deleting_the_last_passkey_is_refused_when_no_recovery_wrapper_exists()
    {
        await AddUser("last@example.test", (FirstCredentialId, "Only", null));
        await SignIn("last@example.test", FirstCredentialId);

        var response = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode(FirstCredentialId)}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("LastVaultWrapper");
        (await PasskeysFor("LAST@EXAMPLE.TEST")).Should().HaveCount(1);
        (await WrappersFor("LAST@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().HaveCount(1);
    }

    [TestCase(VaultWrapperKind.RecoveryPassword)]
    [TestCase(VaultWrapperKind.RecoveryFile)]
    public async Task Deleting_the_last_passkey_is_allowed_when_a_recovery_wrapper_exists(VaultWrapperKind kind)
    {
        await AddUser($"recovery-{kind}@example.test", (FirstCredentialId, "Only", null));
        await AddRecoveryWrapper($"RECOVERY-{kind.ToString().ToUpperInvariant()}@EXAMPLE.TEST", kind);
        await SignIn($"recovery-{kind}@example.test", FirstCredentialId);

        var response = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode(FirstCredentialId)}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PasskeysFor($"RECOVERY-{kind.ToString().ToUpperInvariant()}@EXAMPLE.TEST")).Should().BeEmpty();
        (await WrappersFor($"RECOVERY-{kind.ToString().ToUpperInvariant()}@EXAMPLE.TEST", VaultWrapperKind.Passkey)).Should().BeEmpty();
        (await WrappersFor($"RECOVERY-{kind.ToString().ToUpperInvariant()}@EXAMPLE.TEST", kind)).Should().HaveCount(1);
    }

    [Test]
    public async Task Deleting_another_users_passkey_returns_the_same_404_as_a_missing_passkey()
    {
        await AddUser("delete-owner@example.test", (FirstCredentialId, "Owner", null));
        await AddUser("delete-other@example.test", (SecondCredentialId, "Other", null));
        await SignIn("delete-owner@example.test", FirstCredentialId);

        var other = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode(SecondCredentialId)}");
        var missing = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode([31, 30, 29, 28])}");

        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Content.ReadAsByteArrayAsync()).Should().Equal(await missing.Content.ReadAsByteArrayAsync());
        (await PasskeysFor("DELETE-OTHER@EXAMPLE.TEST")).Should().HaveCount(1);
    }

    [Test]
    public async Task Deleting_a_passkey_without_its_wrapper_returns_404_without_deleting_the_credential()
    {
        await AddUser("orphan@example.test", (FirstCredentialId, "Only", null));
        await SignIn("orphan@example.test", FirstCredentialId);
        await RemoveWrapper("ORPHAN@EXAMPLE.TEST", FirstCredentialId);

        var response = await SendMutation(
            client,
            HttpMethod.Delete,
            $"/api/v1/users/me/passkeys/{WebEncoders.Base64UrlEncode(FirstCredentialId)}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PasskeysFor("ORPHAN@EXAMPLE.TEST")).Should().HaveCount(1);
    }

    private async Task AddUser(
        string email,
        params (byte[] CredentialId, string Label, DateTime? LastUsedAt)[] passkeys)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<XpenseUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var user = new XpenseUser
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            State = AccountState.Active,
            CreatedAt = DateTime.UtcNow
        };
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        foreach (var passkey in passkeys)
        {
            (await userManager.AddOrUpdatePasskeyAsync(user, SoftwareAuthenticator.Passkey(passkey.CredentialId)))
                .Succeeded.Should().BeTrue();
            dbContext.VaultWrappers.Add(Wrapper(user.Id, passkey.CredentialId, passkey.Label, passkey.LastUsedAt));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SignIn(string email, byte[] credentialId, HttpClient? httpClient = null)
    {
        var selectedClient = httpClient ?? client;
        var optionsResponse = await selectedClient.PostAsJsonAsync(
            "/api/v1/auth/passkey/options",
            new { Email = email });
        optionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var optionsBody = await JsonDocument.ParseAsync(await optionsResponse.Content.ReadAsStreamAsync());
        var optionsJson = optionsBody.RootElement.GetProperty("optionsJson").GetString()!;
        var pendingPasskeyAssertionId = optionsBody.RootElement.GetProperty("pendingPasskeyAssertionId").GetGuid();
        var response = await selectedClient.PostAsJsonAsync(
            "/api/v1/auth/passkey/sign-in",
            new
            {
                PendingPasskeyAssertionId = pendingPasskeyAssertionId,
                CredentialJson = SoftwareAuthenticator.CreateAssertion(optionsJson, credentialId)
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task Expire(Guid pendingRegistrationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var pending = await dbContext.PendingRegistrations.SingleAsync(item => item.Id == pendingRegistrationId);
        pending.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();
    }

    private async Task AddRecoveryWrapper(string normalizedEmail, VaultWrapperKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = await dbContext.Users
            .Where(item => item.NormalizedEmail == normalizedEmail)
            .Select(item => item.Id)
            .SingleAsync();
        dbContext.VaultWrappers.Add(new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Salt = [51, 52, 53],
            Ciphertext = [54, 55, 56],
            Nonce = [57, 58, 59],
            AuthenticationTokenHash = kind == VaultWrapperKind.RecoveryFile ? [61, 62, 63] : null,
            ProtocolVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task RemoveWrapper(string normalizedEmail, byte[] credentialId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var wrapper = await dbContext.VaultWrappers.SingleAsync(item =>
            item.UserId == dbContext.Users
                .Where(user => user.NormalizedEmail == normalizedEmail)
                .Select(user => user.Id)
                .Single() &&
            item.CredentialId != null &&
            item.CredentialId.SequenceEqual(credentialId));
        dbContext.VaultWrappers.Remove(wrapper);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<OptionsResponse> CreateManagementOptions(HttpClient httpClient)
    {
        var response = await SendMutation(
            httpClient,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys/options");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<OptionsResponse>())!;
    }

    private static Task<HttpResponseMessage> AddPasskey(
        HttpClient httpClient,
        OptionsResponse options,
        byte[] credentialId,
        VaultWrapperRequest wrapper) =>
        SendMutation(
            httpClient,
            HttpMethod.Post,
            "/api/v1/users/me/passkeys",
            new AddPasskeyRequest(
                options.PendingRegistrationId,
                SoftwareAuthenticator.CreateCredential(options.OptionsJson, credentialId),
                wrapper,
                1));

    private static async Task<HttpResponseMessage> SendMutation(
        HttpClient httpClient,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var tokenResponse = await httpClient.GetAsync("/api/v1/auth/antiforgery");
        var token = (await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryResponse>())!.RequestToken;
        using var request = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        request.Headers.Add("X-Xpense-Antiforgery", token);
        return await httpClient.SendAsync(request);
    }

    private async Task<IList<UserPasskeyInfo>> PasskeysFor(string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<XpenseUser>>();
        var user = await userManager.Users.SingleAsync(item => item.NormalizedEmail == normalizedEmail);
        return await userManager.GetPasskeysAsync(user);
    }

    private async Task<List<VaultWrapper>> WrappersFor(string normalizedEmail, VaultWrapperKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = await dbContext.Users
            .Where(item => item.NormalizedEmail == normalizedEmail)
            .Select(item => item.Id)
            .SingleAsync();
        return await dbContext.VaultWrappers
            .Where(wrapper => wrapper.UserId == userId && wrapper.Kind == kind)
            .ToListAsync();
    }

    private static VaultWrapper Wrapper(
        Guid userId,
        byte[] credentialId,
        string label,
        DateTime? lastUsedAt = null) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Kind = VaultWrapperKind.Passkey,
        CredentialId = credentialId,
        Salt = [31, 32, 33],
        Ciphertext = [34, 35, 36],
        Nonce = [37, 38, 39],
        ProtocolVersion = 1,
        Label = label,
        LastUsedAt = lastUsedAt,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed record PasskeyResponse(string CredentialId, string? Label, DateTime? LastUsedAt);

    private sealed record OptionsResponse(string OptionsJson, Guid PendingRegistrationId);

    private sealed record AntiforgeryResponse(string RequestToken);

    private sealed record AddPasskeyRequest(
        Guid PendingRegistrationId,
        string CredentialJson,
        VaultWrapperRequest? VaultWrapper,
        int ProtocolVersion);

    private sealed record VaultWrapperRequest(
        string? Salt,
        string? Ciphertext,
        string? Nonce,
        string? Label);

    private static VaultWrapperRequest ValidWrapper { get; } = new(
        Convert.ToBase64String([41, 42, 43]),
        Convert.ToBase64String([44, 45, 46]),
        Convert.ToBase64String([47, 48, 49]),
        "Phone");
}
