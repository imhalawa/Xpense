using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;
using Xpense.Tests.Infrastructure;

namespace Xpense.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class AuthenticationEndpointTests
{
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
    public async Task Requesting_registration_options_returns_a_challenge_and_a_pending_registration()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest("new@example.test"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var registration = await ReadOptions(response);
        registration.OptionsJson.Should().Contain("challenge");

        await using var scope = factory.Services.CreateAsyncScope();
        var pending = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .PendingRegistrations.SingleAsync(item => item.Id == registration.PendingRegistrationId);
        pending.NormalizedEmail.Should().Be("NEW@EXAMPLE.TEST");
        pending.ConsumedAt.Should().BeNull();
        pending.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Requesting_registration_options_for_a_taken_email_looks_the_same_as_for_a_free_one()
    {
        await AddUser("taken@example.test");

        var freeResponse = await client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest("free@example.test"));
        var takenResponse = await client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest("taken@example.test"));

        freeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        takenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonDocument.ParseAsync(await freeResponse.Content.ReadAsStreamAsync())).RootElement
            .EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
                (await JsonDocument.ParseAsync(await takenResponse.Content.ReadAsStreamAsync())).RootElement
                .EnumerateObject().Select(property => property.Name));
    }

    [Test]
    public async Task Requesting_registration_options_again_replaces_the_prior_pending_registration()
    {
        var first = await CreateOptions("replace@example.test");
        var second = await CreateOptions("replace@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var registrations = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .PendingRegistrations.Where(item => item.NormalizedEmail == "REPLACE@EXAMPLE.TEST").ToListAsync();
        registrations.Should().HaveCount(2);
        registrations.Single(item => item.Id == first.PendingRegistrationId).ConsumedAt.Should().NotBeNull();
        registrations.Single(item => item.Id == second.PendingRegistrationId).ConsumedAt.Should().BeNull();

        (await Register(first)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Register(second)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Concurrent_registration_options_leave_one_usable_pending_registration()
    {
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest("concurrent@example.test")),
            client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest("concurrent@example.test")));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        var registrations = await Task.WhenAll(responses.Select(ReadOptions));
        await using var scope = factory.Services.CreateAsyncScope();
        var pending = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .PendingRegistrations.Where(item => item.NormalizedEmail == "CONCURRENT@EXAMPLE.TEST").ToListAsync();
        pending.Should().HaveCount(2);
        pending.Count(item => item.ConsumedAt is null).Should().Be(1);

        var usable = registrations.Single(registration => pending.Single(item => item.Id == registration.PendingRegistrationId).ConsumedAt is null);
        (await Register(usable)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Requesting_registration_options_replaces_an_expired_registration()
    {
        var first = await CreateOptions("expired-options@example.test");
        await Expire(first.PendingRegistrationId);

        var second = await CreateOptions("expired-options@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var registrations = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .PendingRegistrations.Where(item => item.NormalizedEmail == "EXPIRED-OPTIONS@EXAMPLE.TEST").ToListAsync();
        registrations.Single(item => item.Id == first.PendingRegistrationId).ConsumedAt.Should().NotBeNull();
        registrations.Single(item => item.Id == second.PendingRegistrationId).ConsumedAt.Should().BeNull();
        (await Register(second)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Registration_creates_the_user_the_encryption_identity_and_the_vault_wrapper_together()
    {
        var registration = await CreateOptions("complete@example.test");

        var response = await Register(registration);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.IsAbsoluteUri.Should().BeTrue();
        response.Headers.Location!.AbsolutePath.Should().Be("/api/v1/auth/me");
        response.Headers.TryGetValues("Set-Cookie", out _).Should().BeTrue();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.NormalizedEmail == "COMPLETE@EXAMPLE.TEST");
        user.State.Should().Be(AccountState.Active);
        (await dbContext.UserEncryptionIdentities.CountAsync(item => item.UserId == user.Id)).Should().Be(1);
        var wrapper = await dbContext.VaultWrappers.SingleAsync(item => item.UserId == user.Id);
        wrapper.Kind.Should().Be(VaultWrapperKind.Passkey);
        wrapper.CredentialId.Should().Equal([1, 2, 3, 4]);
    }

    [Test]
    public async Task Registration_without_a_vault_wrapper_creates_no_user()
    {
        var registration = await CreateOptions("no-wrapper@example.test");

        var response = await Register(registration, omitWrapper: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("NO-WRAPPER@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_without_an_encryption_public_key_creates_no_user()
    {
        var registration = await CreateOptions("no-public-key@example.test");

        var response = await Register(registration, encryptionPublicKey: string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("NO-PUBLIC-KEY@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_with_an_expired_pending_registration_creates_no_user()
    {
        var registration = await CreateOptions("expired@example.test");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var pending = await dbContext.PendingRegistrations.SingleAsync(item => item.Id == registration.PendingRegistrationId);
            pending.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await Register(registration);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("EXPIRED@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_replaying_a_consumed_challenge_creates_no_second_user()
    {
        var registration = await CreateOptions("replayed@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);

        var replay = await Register(registration);

        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .Users.CountAsync(item => item.NormalizedEmail == "REPLAYED@EXAMPLE.TEST")).Should().Be(1);
    }

    [Test]
    public async Task Registration_with_an_invalid_attestation_creates_no_user()
    {
        var registration = await CreateOptions("invalid-attestation@example.test");

        var response = await Register(registration, credentialJson: "invalid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("INVALID-ATTESTATION@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_cannot_pair_a_challenge_with_another_pending_registration()
    {
        var first = await CreateOptions("first-challenge@example.test");
        var second = await CreateOptions("second-challenge@example.test");

        var response = await Register(first, credentialJson: SoftwareAuthenticator.CreateCredential(second.OptionsJson));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("FIRST-CHALLENGE@EXAMPLE.TEST");
        await AssertNoUser("SECOND-CHALLENGE@EXAMPLE.TEST");
    }

    [TestCase("encryptionPublicKey")]
    [TestCase("encryptedPrivateKey")]
    [TestCase("encryptedPrivateKeyNonce")]
    [TestCase("wrapperSalt")]
    [TestCase("wrapperCiphertext")]
    [TestCase("wrapperNonce")]
    public async Task Registration_with_a_null_binary_field_creates_no_user(string field)
    {
        await AssertInvalidBinary(field, null);
    }

    [TestCase("encryptionPublicKey")]
    [TestCase("encryptedPrivateKey")]
    [TestCase("encryptedPrivateKeyNonce")]
    [TestCase("wrapperSalt")]
    [TestCase("wrapperCiphertext")]
    [TestCase("wrapperNonce")]
    public async Task Registration_with_an_invalid_base64_field_creates_no_user(string field)
    {
        await AssertInvalidBinary(field, "not-base64");
    }

    [TestCase("encryptionPublicKey")]
    [TestCase("encryptedPrivateKey")]
    [TestCase("encryptedPrivateKeyNonce")]
    [TestCase("wrapperSalt")]
    [TestCase("wrapperCiphertext")]
    [TestCase("wrapperNonce")]
    public async Task Registration_with_an_oversized_binary_field_creates_no_user(string field)
    {
        await AssertInvalidBinary(field, Convert.ToBase64String(new byte[4097]));
    }

    [Test]
    public async Task Registration_with_an_unknown_protocol_version_creates_no_user()
    {
        var registration = await CreateOptions("protocol@example.test");

        var response = await Register(registration, protocolVersion: 2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("PROTOCOL@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_is_refused_when_the_policy_is_closed()
    {
        await factory.DisposeAsync();
        factory = new WebApiTestFactory(await PostgresFixture.CreateDatabase())
            .WithRegistrationPolicy(RegistrationPolicy.Closed);
        client.Dispose();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var registration = await CreateOptions("closed@example.test");

        var response = await Register(registration);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("CLOSED@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_is_refused_without_a_valid_invitation_when_the_policy_is_invite_only()
    {
        await RestartWithPolicy(RegistrationPolicy.InviteOnly);
        var registration = await CreateOptions("uninvited@example.test");

        var response = await Register(registration);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("UNINVITED@EXAMPLE.TEST");
    }

    [Test]
    public async Task Registration_accepts_a_valid_invitation_when_the_policy_is_invite_only()
    {
        await RestartWithPolicy(RegistrationPolicy.InviteOnly);
        const string token = "valid-invitation-token";
        await AddInvitation(token, "INVITED@EXAMPLE.TEST");
        var registration = await CreateOptions("invited@example.test");

        var response = await Register(registration, invitationToken: token);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Registration_is_refused_with_an_invalid_invitation_when_the_policy_is_invite_only()
    {
        await RestartWithPolicy(RegistrationPolicy.InviteOnly);
        const string token = "valid-invitation-token";
        await AddInvitation(token, "INVITED@EXAMPLE.TEST");
        var registration = await CreateOptions("invited@example.test");

        var response = await Register(registration, invitationToken: "invalid-invitation-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser("INVITED@EXAMPLE.TEST");
    }

    [Test]
    public async Task Signing_in_with_a_registered_passkey_sets_the_session_cookie()
    {
        var registration = await CreateOptions("sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "sign-in@example.test");

        var response = await SignIn(anonymousClient, options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(cookie => cookie.StartsWith("xpense.session=", StringComparison.Ordinal));
    }

    [Test]
    public async Task Passkey_options_do_not_reveal_whether_the_email_exists()
    {
        var registration = await CreateOptions("known-options@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var known = await CreatePasskeyOptions(anonymousClient, "known-options@example.test");
        var unknown = await CreatePasskeyOptions(anonymousClient, "unknown-options@example.test");
        var discoverable = await CreatePasskeyOptions(anonymousClient, null);

        NormalizeChallenge(known.OptionsJson).Should().Be(NormalizeChallenge(unknown.OptionsJson));
        NormalizeChallenge(known.OptionsJson).Should().Be(NormalizeChallenge(discoverable.OptionsJson));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var assertions = await dbContext.PendingPasskeyAssertions
            .Where(assertion => new[]
            {
                known.PendingPasskeyAssertionId,
                unknown.PendingPasskeyAssertionId,
                discoverable.PendingPasskeyAssertionId
            }.Contains(assertion.Id))
            .ToListAsync();
        assertions.Single(assertion => assertion.Id == known.PendingPasskeyAssertionId).NormalizedEmail.Should().Be("KNOWN-OPTIONS@EXAMPLE.TEST");
        assertions.Single(assertion => assertion.Id == unknown.PendingPasskeyAssertionId).NormalizedEmail.Should().Be("UNKNOWN-OPTIONS@EXAMPLE.TEST");
        assertions.Single(assertion => assertion.Id == discoverable.PendingPasskeyAssertionId).NormalizedEmail.Should().BeNull();
    }

    [Test]
    public async Task Signing_in_with_a_credential_belonging_to_another_user_than_the_email_hint_is_refused_neutrally()
    {
        var registration = await CreateOptions("hinted-user@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        await AddIdentityUser("other-user@example.test");
        var otherCredentialId = new byte[] { 9, 8, 7, 6 };
        await AddPasskey("OTHER-USER@EXAMPLE.TEST", otherCredentialId);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var mismatchedOptions = await CreatePasskeyOptions(anonymousClient, "hinted-user@example.test");
        var unknownOptions = await CreatePasskeyOptions(anonymousClient, "unknown-user@example.test");
        var invalidOptions = await CreatePasskeyOptions(anonymousClient, "hinted-user@example.test");

        var mismatched = await SignIn(
            anonymousClient,
            mismatchedOptions,
            SoftwareAuthenticator.CreateAssertion(mismatchedOptions.OptionsJson, otherCredentialId));
        var unknown = await SignIn(
            anonymousClient,
            unknownOptions,
            SoftwareAuthenticator.CreateAssertion(unknownOptions.OptionsJson, otherCredentialId));
        var invalid = await SignIn(anonymousClient, invalidOptions, "not-an-assertion");

        mismatched.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await mismatched.Content.ReadAsStringAsync()).Should().Be(await invalid.Content.ReadAsStringAsync());
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await invalid.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var assertion = await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .PendingPasskeyAssertions.SingleAsync(item => item.Id == mismatchedOptions.PendingPasskeyAssertionId);
        assertion.ConsumedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Signing_in_returns_the_users_vault_wrapper_so_the_client_can_unlock()
    {
        var registration = await CreateOptions("wrapper-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "wrapper-sign-in@example.test");

        var response = await SignIn(anonymousClient, options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("vaultState").GetInt32().Should().Be(1);
        var wrapper = body.RootElement.GetProperty("vaultWrappers").EnumerateArray().Single();
        wrapper.GetProperty("credentialId").GetString().Should().Be(Convert.ToBase64String([1, 2, 3, 4]));
        wrapper.GetProperty("salt").GetString().Should().Be(Convert.ToBase64String([10, 11, 12]));
        wrapper.GetProperty("ciphertext").GetString().Should().Be(Convert.ToBase64String([13, 14, 15]));
        wrapper.GetProperty("nonce").GetString().Should().Be(Convert.ToBase64String([16, 17, 18]));

        var wrapperInDatabase = await WrapperFor("WRAPPER-SIGN-IN@EXAMPLE.TEST");
        wrapperInDatabase.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Signing_in_with_a_second_registered_passkey_also_works()
    {
        var registration = await CreateOptions("second-passkey@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        var secondCredentialId = new byte[] { 9, 8, 7, 6 };
        await AddPasskey("SECOND-PASSKEY@EXAMPLE.TEST", secondCredentialId);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "second-passkey@example.test");

        var response = await SignIn(anonymousClient, options, SoftwareAuthenticator.CreateAssertion(options.OptionsJson, secondCredentialId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("vaultState").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("vaultWrappers").GetArrayLength().Should().Be(2);
    }

    [Test]
    public async Task Signing_in_with_an_unknown_credential_returns_the_same_response_as_a_wrong_one()
    {
        var registration = await CreateOptions("neutral-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var unknownOptions = await CreatePasskeyOptions(anonymousClient, "neutral-sign-in@example.test");
        var wrongOptions = await CreatePasskeyOptions(anonymousClient, "neutral-sign-in@example.test");

        var unknown = await SignIn(anonymousClient, unknownOptions, SoftwareAuthenticator.CreateAssertion(unknownOptions.OptionsJson, [9, 8, 7, 6]));
        var wrong = await SignIn(anonymousClient, wrongOptions, "not-an-assertion");

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await wrong.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Signing_in_with_an_expired_challenge_is_refused()
    {
        var registration = await CreateOptions("expired-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "expired-sign-in@example.test");
        await ExpirePasskeyAssertion(options.PendingPasskeyAssertionId);

        var response = await SignIn(anonymousClient, options);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Replaying_a_consumed_assertion_is_refused()
    {
        var registration = await CreateOptions("replay-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "replay-sign-in@example.test");

        (await SignIn(anonymousClient, options)).StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = await SignIn(anonymousClient, options);

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Signing_in_when_no_vault_wrapper_matches_the_credential_still_authenticates()
    {
        var registration = await CreateOptions("locked-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        await ChangeWrapperCredential("LOCKED-SIGN-IN@EXAMPLE.TEST", [9, 8, 7, 6]);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(anonymousClient, "locked-sign-in@example.test");

        var response = await SignIn(anonymousClient, options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("vaultState").GetInt32().Should().Be(0);
        response.Headers.TryGetValues("Set-Cookie", out _).Should().BeTrue();
    }

    [Test]
    public async Task Signing_in_with_an_assertion_from_another_ceremony_is_refused()
    {
        var registration = await CreateOptions("mismatched-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var first = await CreatePasskeyOptions(anonymousClient, "mismatched-sign-in@example.test");
        var second = await CreatePasskeyOptions(anonymousClient, "mismatched-sign-in@example.test");

        var response = await SignIn(anonymousClient, second, SoftwareAuthenticator.CreateAssertion(first.OptionsJson));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Concurrent_sign_in_attempts_consume_an_assertion_once()
    {
        var registration = await CreateOptions("concurrent-sign-in@example.test");
        (await Register(registration)).StatusCode.Should().Be(HttpStatusCode.Created);
        using var firstClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        using var secondClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        var options = await CreatePasskeyOptions(firstClient, "concurrent-sign-in@example.test");

        var responses = await Task.WhenAll(SignIn(firstClient, options), SignIn(secondClient, options));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
    }

    [Test]
    public async Task Recovery_password_sign_in_works_when_the_user_configured_one()
    {
        await AddRecoveryPassword("recovery-password@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var response = await SignInWithRecoveryPassword(
            anonymousClient,
            "recovery-password@example.test",
            "A memorable recovery password 1!");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(cookie => cookie.StartsWith("xpense.session=", StringComparison.Ordinal));

        var wrapper = await RecoveryWrapperFor("RECOVERY-PASSWORD@EXAMPLE.TEST", VaultWrapperKind.RecoveryPassword);
        wrapper.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Recovery_password_sign_in_returns_the_password_wrapper_with_its_argon2_parameters()
    {
        await AddRecoveryPassword("recovery-password-wrapper@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var response = await SignInWithRecoveryPassword(
            anonymousClient,
            "recovery-password-wrapper@example.test",
            "A memorable recovery password 1!");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("vaultState").GetInt32().Should().Be(1);
        var wrapper = body.RootElement.GetProperty("vaultWrappers").EnumerateArray().Single();
        wrapper.GetProperty("kind").GetInt32().Should().Be((int)VaultWrapperKind.RecoveryPassword);
        wrapper.GetProperty("salt").GetString().Should().Be(Convert.ToBase64String([31, 32, 33]));
        wrapper.GetProperty("ciphertext").GetString().Should().Be(Convert.ToBase64String([34, 35, 36]));
        wrapper.GetProperty("nonce").GetString().Should().Be(Convert.ToBase64String([37, 38, 39]));
        using var parameters = JsonDocument.Parse(wrapper.GetProperty("parameters").GetString()!);
        parameters.RootElement.GetProperty("memoryKibibytes").GetInt32().Should().Be(65_536);
        parameters.RootElement.GetProperty("iterations").GetInt32().Should().Be(3);
        parameters.RootElement.GetProperty("lanes").GetInt32().Should().Be(4);
        parameters.RootElement.GetProperty("hashLength").GetInt32().Should().Be(32);
    }

    [Test]
    public async Task Recovery_password_sign_in_for_an_unknown_email_looks_identical_to_a_wrong_password()
    {
        await AddRecoveryPassword("recovery-password-neutral@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var unknown = await SignInWithRecoveryPassword(
            anonymousClient,
            "unknown-recovery-password@example.test",
            "A memorable recovery password 1!");
        var wrong = await SignInWithRecoveryPassword(
            anonymousClient,
            "recovery-password-neutral@example.test",
            "a wrong recovery password");

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsByteArrayAsync()).Should().Equal(await wrong.Content.ReadAsByteArrayAsync());
    }

    [Test]
    public async Task Recovery_password_sign_in_is_refused_when_no_recovery_password_is_configured()
    {
        await AddPasswordUser("no-recovery-password@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var response = await SignInWithRecoveryPassword(
            anonymousClient,
            "no-recovery-password@example.test",
            "A memorable recovery password 1!");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Recovery_password_sign_in_counts_failed_attempts_for_lockout()
    {
        await AddRecoveryPassword("recovery-password-lockout@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var response = await SignInWithRecoveryPassword(
            anonymousClient,
            "recovery-password-lockout@example.test",
            "A wrong recovery password 1!");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var user = await UserFor("RECOVERY-PASSWORD-LOCKOUT@EXAMPLE.TEST");
        user.AccessFailedCount.Should().Be(1);
    }

    [Test]
    public async Task Recovery_password_failure_paths_each_verify_one_hash_and_only_the_real_user_counts_for_lockout()
    {
        var passwordHasher = new CountingFailedPasswordHasher();
        await RestartWithPasswordHasher(passwordHasher);
        await AddRecoveryPassword("recovery-password-counted@example.test", "A memorable recovery password 1!");
        await AddPasswordUser("no-recovery-password-counted@example.test", "A memorable recovery password 1!");
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var wrong = await SignInWithRecoveryPassword(
            anonymousClient,
            "recovery-password-counted@example.test",
            "A wrong recovery password 1!");
        passwordHasher.VerificationCount.Should().Be(1);
        var unknown = await SignInWithRecoveryPassword(
            anonymousClient,
            "unknown-recovery-password-counted@example.test",
            "A wrong recovery password 1!");
        passwordHasher.VerificationCount.Should().Be(2);
        var noWrapper = await SignInWithRecoveryPassword(
            anonymousClient,
            "no-recovery-password-counted@example.test",
            "A wrong recovery password 1!");
        passwordHasher.VerificationCount.Should().Be(3);

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        noWrapper.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await UserFor("RECOVERY-PASSWORD-COUNTED@EXAMPLE.TEST")).AccessFailedCount.Should().Be(1);
        (await UserFor("NO-RECOVERY-PASSWORD-COUNTED@EXAMPLE.TEST")).AccessFailedCount.Should().Be(0);
        (await wrong.Content.ReadAsByteArrayAsync()).Should().Equal(await unknown.Content.ReadAsByteArrayAsync());
        (await wrong.Content.ReadAsByteArrayAsync()).Should().Equal(await noWrapper.Content.ReadAsByteArrayAsync());
    }

    [Test]
    public async Task Recovery_password_dummy_verifications_use_separate_synthetic_users_under_concurrency()
    {
        var passwordHasher = new CountingFailedPasswordHasher();
        await RestartWithPasswordHasher(passwordHasher);
        await AddPasswordUser("no-recovery-password-concurrent@example.test", "A memorable recovery password 1!");
        using var firstClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        using var secondClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var responses = await Task.WhenAll(
            SignInWithRecoveryPassword(
                firstClient,
                "unknown-recovery-password-concurrent@example.test",
                "A wrong recovery password 1!"),
            SignInWithRecoveryPassword(
                secondClient,
                "no-recovery-password-concurrent@example.test",
                "A wrong recovery password 1!"));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
        var verifiedUsers = passwordHasher.VerifiedUsers.ToArray();
        verifiedUsers.Should().HaveCount(2);
        ReferenceEquals(verifiedUsers[0], verifiedUsers[1]).Should().BeFalse();
        verifiedUsers.Should().OnlyContain(user => user.Id == Guid.Empty);
        (await UserFor("NO-RECOVERY-PASSWORD-CONCURRENT@EXAMPLE.TEST")).AccessFailedCount.Should().Be(0);
    }

    [Test]
    public async Task Recovery_file_sign_in_consumes_the_token_and_a_second_attempt_is_refused()
    {
        const string authenticationToken = "recovery-file-authentication-token";
        await AddRecoveryFile("recovery-file@example.test", authenticationToken);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var first = await SignInWithRecoveryFile(anonymousClient, authenticationToken);
        var second = await SignInWithRecoveryFile(anonymousClient, authenticationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies.Should().Contain(cookie => cookie.StartsWith("xpense.session=", StringComparison.Ordinal));
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var wrapper = await RecoveryWrapperFor("RECOVERY-FILE@EXAMPLE.TEST", VaultWrapperKind.RecoveryFile);
        wrapper.ConsumedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        wrapper.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Recovery_file_sign_in_with_an_unknown_token_is_refused_neutrally()
    {
        const string authenticationToken = "known-recovery-file-authentication-token";
        await AddRecoveryFile("recovery-file-neutral@example.test", authenticationToken);
        using var anonymousClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var unknown = await SignInWithRecoveryFile(anonymousClient, "unknown-recovery-file-authentication-token");
        var invalid = await SignInWithRecoveryFile(anonymousClient, authenticationToken);
        var replay = await SignInWithRecoveryFile(anonymousClient, authenticationToken);

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        invalid.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsByteArrayAsync()).Should().Equal(await replay.Content.ReadAsByteArrayAsync());
    }

    [Test]
    public async Task Concurrent_recovery_file_sign_in_attempts_consume_a_token_once()
    {
        const string authenticationToken = "concurrent-recovery-file-authentication-token";
        await AddRecoveryFile("concurrent-recovery-file@example.test", authenticationToken);
        using var firstClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
        using var secondClient = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });

        var responses = await Task.WhenAll(
            SignInWithRecoveryFile(firstClient, authenticationToken),
            SignInWithRecoveryFile(secondClient, authenticationToken));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
    }

    [TestCase(null, "A memorable recovery password 1!")]
    [TestCase("recovery-password-input@example.test", null)]
    public async Task Recovery_password_sign_in_requires_an_email_and_password(string? email, string? password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password/sign-in",
            new RecoveryPasswordSignInRequest(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task Recovery_file_sign_in_requires_an_authentication_token(string? authenticationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/recovery/sign-in",
            new RecoveryFileSignInRequest(authenticationToken));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Eleven_sign_in_attempts_in_a_minute_returns_429()
    {
        var responses = new List<HttpResponseMessage>();

        for (var attempt = 0; attempt < 11; attempt++)
            responses.Add(await client.PostAsJsonAsync("/api/v1/auth/passkey/options", new PasskeyOptionsRequest(null)));

        responses.Take(10).Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        responses[10].StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private async Task<OptionsResponse> CreateOptions(string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register/options", new OptionsRequest(email));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadOptions(response);
    }

    private static async Task<OptionsResponse> ReadOptions(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<OptionsResponse>())!;

    private static async Task<PasskeyOptionsResponse> CreatePasskeyOptions(HttpClient httpClient, string? email)
    {
        var response = await httpClient.PostAsJsonAsync("/api/v1/auth/passkey/options", new PasskeyOptionsRequest(email));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PasskeyOptionsResponse>())!;
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient httpClient, PasskeyOptionsResponse options, string? credentialJson = null) =>
        httpClient.PostAsJsonAsync("/api/v1/auth/passkey/sign-in", new PasskeySignInRequest(
            options.PendingPasskeyAssertionId,
            credentialJson ?? SoftwareAuthenticator.CreateAssertion(options.OptionsJson)));

    private static Task<HttpResponseMessage> SignInWithRecoveryPassword(HttpClient httpClient, string? email, string? password) =>
        httpClient.PostAsJsonAsync("/api/v1/auth/password/sign-in", new RecoveryPasswordSignInRequest(email, password));

    private static Task<HttpResponseMessage> SignInWithRecoveryFile(HttpClient httpClient, string? authenticationToken) =>
        httpClient.PostAsJsonAsync("/api/v1/auth/recovery/sign-in", new RecoveryFileSignInRequest(authenticationToken));

    private static string NormalizeChallenge(string optionsJson)
    {
        var options = JsonNode.Parse(optionsJson)!.AsObject();
        options["challenge"] = "normalized";
        return options.ToJsonString();
    }

    private Task<HttpResponseMessage> Register(
        OptionsResponse registration,
        string? credentialJson = null,
        string? encryptionPublicKey = null,
        VaultWrapperRequest? wrapper = null,
        bool omitWrapper = false,
        string? invitationToken = null,
        int protocolVersion = 1,
        string? encryptedPrivateKey = null,
        string? encryptedPrivateKeyNonce = null) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            registration.PendingRegistrationId,
            credentialJson ?? SoftwareAuthenticator.CreateCredential(registration.OptionsJson),
            encryptionPublicKey ?? Convert.ToBase64String([1, 2, 3]),
            encryptedPrivateKey ?? Convert.ToBase64String([4, 5, 6]),
            encryptedPrivateKeyNonce ?? Convert.ToBase64String([7, 8, 9]),
            omitWrapper ? null : wrapper ?? VaultWrapperRequest.Valid,
            protocolVersion,
            invitationToken));

    private async Task AssertInvalidBinary(string field, string? value)
    {
        var email = field + "-invalid@example.test";
        var registration = await CreateOptions(email);
        var wrapper = VaultWrapperRequest.Valid;
        var request = new RegisterRequest(
            registration.PendingRegistrationId,
            SoftwareAuthenticator.CreateCredential(registration.OptionsJson),
            field == "encryptionPublicKey" ? value : Convert.ToBase64String([1]),
            field == "encryptedPrivateKey" ? value : Convert.ToBase64String([2]),
            field == "encryptedPrivateKeyNonce" ? value : Convert.ToBase64String([3]),
            wrapper with
            {
                Salt = field == "wrapperSalt" ? value : wrapper.Salt,
                Ciphertext = field == "wrapperCiphertext" ? value : wrapper.Ciphertext,
                Nonce = field == "wrapperNonce" ? value : wrapper.Nonce
            },
            1,
            null);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoUser(email.ToUpperInvariant());
    }

    private async Task Expire(Guid pendingRegistrationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var pending = await dbContext.PendingRegistrations.SingleAsync(item => item.Id == pendingRegistrationId);
        pending.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();
    }

    private async Task ExpirePasskeyAssertion(Guid pendingPasskeyAssertionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var assertion = await dbContext.PendingPasskeyAssertions.SingleAsync(item => item.Id == pendingPasskeyAssertionId);
        assertion.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();
    }

    private async Task<VaultWrapper> WrapperFor(string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = await dbContext.Users.Where(item => item.NormalizedEmail == normalizedEmail).Select(item => item.Id).SingleAsync();
        return await dbContext.VaultWrappers.SingleAsync(item => item.UserId == userId);
    }

    private async Task<VaultWrapper> RecoveryWrapperFor(string normalizedEmail, VaultWrapperKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = await dbContext.Users.Where(item => item.NormalizedEmail == normalizedEmail).Select(item => item.Id).SingleAsync();
        return await dbContext.VaultWrappers.SingleAsync(item => item.UserId == userId && item.Kind == kind);
    }

    private async Task<XpenseUser> UserFor(string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .Users.SingleAsync(item => item.NormalizedEmail == normalizedEmail);
    }

    private async Task AddPasskey(string normalizedEmail, byte[] credentialId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.NormalizedEmail == normalizedEmail);
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<XpenseUser>>();
        (await userManager.AddOrUpdatePasskeyAsync(user, SoftwareAuthenticator.Passkey(credentialId))).Succeeded.Should().BeTrue();
        dbContext.VaultWrappers.Add(new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = VaultWrapperKind.Passkey,
            CredentialId = credentialId,
            Salt = [20, 21, 22],
            Ciphertext = [23, 24, 25],
            Nonce = [26, 27, 28],
            ProtocolVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task ChangeWrapperCredential(string normalizedEmail, byte[] credentialId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var userId = await dbContext.Users.Where(item => item.NormalizedEmail == normalizedEmail).Select(item => item.Id).SingleAsync();
        var wrapper = await dbContext.VaultWrappers.SingleAsync(item => item.UserId == userId);
        wrapper.CredentialId = credentialId;
        await dbContext.SaveChangesAsync();
    }

    private async Task RestartWithPolicy(RegistrationPolicy policy)
    {
        var connectionString = await PostgresFixture.CreateDatabase();
        client.Dispose();
        await factory.DisposeAsync();
        factory = new WebApiTestFactory(connectionString).WithRegistrationPolicy(policy);
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
    }

    private async Task RestartWithPasswordHasher(Microsoft.AspNetCore.Identity.IPasswordHasher<XpenseUser> passwordHasher)
    {
        var connectionString = await PostgresFixture.CreateDatabase();
        client.Dispose();
        await factory.DisposeAsync();
        factory = new WebApiTestFactory(connectionString).WithPasswordHasher(passwordHasher);
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://app.example.test") });
    }

    private async Task AddInvitation(string token, string targetNormalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var owner = User("owner@example.test");
        var group = new Group
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            NameCiphertext = [1],
            NameNonce = [2],
            ProtocolVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(owner);
        dbContext.Groups.Add(group);
        dbContext.GroupInvitations.Add(new GroupInvitation
        {
            Id = Guid.CreateVersion7(),
            GroupId = group.Id,
            InvitedByUserId = owner.Id,
            TargetNormalizedEmail = targetNormalizedEmail,
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            State = InvitationState.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task AddUser(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        dbContext.Users.Add(User(email));
        await dbContext.SaveChangesAsync();
    }

    private async Task AddIdentityUser(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<XpenseUser>>();
        (await userManager.CreateAsync(User(email))).Succeeded.Should().BeTrue();
    }

    private async Task AddPasswordUser(string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<XpenseUser>>();
        var user = User(email);
        user.LockoutEnabled = true;
        (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
    }

    private async Task AddRecoveryPassword(string email, string password)
    {
        await AddPasswordUser(email, password);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.NormalizedEmail == email.ToUpperInvariant());
        dbContext.VaultWrappers.Add(new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = VaultWrapperKind.RecoveryPassword,
            Salt = [31, 32, 33],
            Ciphertext = [34, 35, 36],
            Nonce = [37, 38, 39],
            Parameters = "{\"memoryKibibytes\":65536,\"iterations\":3,\"lanes\":4,\"hashLength\":32}",
            ProtocolVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task AddRecoveryFile(string email, string authenticationToken)
    {
        await AddIdentityUser(email);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.NormalizedEmail == email.ToUpperInvariant());
        dbContext.VaultWrappers.Add(new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = VaultWrapperKind.RecoveryFile,
            Salt = [41, 42, 43],
            Ciphertext = [44, 45, 46],
            Nonce = [47, 48, 49],
            AuthenticationTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(authenticationToken)),
            ProtocolVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static XpenseUser User(string email) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        State = AccountState.Active,
        CreatedAt = DateTime.UtcNow
    };

    private async Task AssertNoUser(string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<XpenseDbContext>()
            .Users.AnyAsync(item => item.NormalizedEmail == normalizedEmail)).Should().BeFalse();
    }

    private sealed record OptionsRequest(string Email);

    private sealed record OptionsResponse(string OptionsJson, Guid PendingRegistrationId);

    private sealed record PasskeyOptionsRequest(string? Email);

    private sealed record PasskeyOptionsResponse(string OptionsJson, Guid PendingPasskeyAssertionId);

    private sealed record PasskeySignInRequest(Guid PendingPasskeyAssertionId, string CredentialJson);

    private sealed record RecoveryPasswordSignInRequest(string? Email, string? Password);

    private sealed record RecoveryFileSignInRequest(string? AuthenticationToken);

    private sealed record RegisterRequest(
        Guid PendingRegistrationId,
        string CredentialJson,
        string? EncryptionPublicKey,
        string? EncryptedPrivateKey,
        string? EncryptedPrivateKeyNonce,
        VaultWrapperRequest? VaultWrapper,
        int ProtocolVersion,
        string? InvitationToken);

    private sealed record VaultWrapperRequest(
        string? Salt,
        string? Ciphertext,
        string? Nonce,
        string? Label)
    {
        public static VaultWrapperRequest Valid { get; } = new(
            Convert.ToBase64String([10, 11, 12]),
            Convert.ToBase64String([13, 14, 15]),
            Convert.ToBase64String([16, 17, 18]),
            "Test passkey");
    }

    private sealed class CountingFailedPasswordHasher : Microsoft.AspNetCore.Identity.IPasswordHasher<XpenseUser>
    {
        private readonly Microsoft.AspNetCore.Identity.PasswordHasher<XpenseUser> passwordHasher = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<XpenseUser> verifiedUsers = new();
        private int verificationCount;

        public int VerificationCount => verificationCount;

        public IReadOnlyCollection<XpenseUser> VerifiedUsers => verifiedUsers;

        public string HashPassword(XpenseUser user, string password) => passwordHasher.HashPassword(user, password);

        public Microsoft.AspNetCore.Identity.PasswordVerificationResult VerifyHashedPassword(
            XpenseUser user,
            string hashedPassword,
            string providedPassword)
        {
            verifiedUsers.Enqueue(user);
            Interlocked.Increment(ref verificationCount);
            return Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
        }
    }
}
