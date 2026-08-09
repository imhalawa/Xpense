using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Xpense.Domain.Entities;

namespace Xpense.Tests.Infrastructure;

public sealed class SoftwareAuthenticator : IPasskeyHandler<XpenseUser>
{
    private static readonly byte[] CredentialId = [1, 2, 3, 4];
    private static readonly byte[] PublicKey = [5, 6, 7, 8];

    public Task<PasskeyCreationOptionsResult> MakeCreationOptionsAsync(
        PasskeyUserEntity userEntity,
        HttpContext httpContext)
    {
        var challenge = Guid.CreateVersion7().ToString();
        return Task.FromResult(new PasskeyCreationOptionsResult
        {
            CreationOptionsJson = "{\"challenge\":\"" + challenge + "\"}",
            AttestationState = challenge + "|" + userEntity.Id + "|" + userEntity.Name
        });
    }

    private readonly UserManager<XpenseUser> userManager;

    public SoftwareAuthenticator(UserManager<XpenseUser> userManager) => this.userManager = userManager;

    public async Task<PasskeyRequestOptionsResult> MakeRequestOptionsAsync(XpenseUser? user, HttpContext httpContext)
    {
        var challenge = Guid.CreateVersion7().ToString();
        var allowCredentials = user is null
            ? []
            : (await userManager.GetPasskeysAsync(user))
                .Select(passkey => Convert.ToBase64String(passkey.CredentialId))
                .Order()
                .ToArray();
        return new PasskeyRequestOptionsResult
        {
            RequestOptionsJson = JsonSerializer.Serialize(new
            {
                challenge,
                rpId = "identity.example.test",
                userVerification = "required",
                allowCredentials
            }),
            AssertionState = challenge
        };
    }

    public Task<PasskeyAttestationResult> PerformAttestationAsync(PasskeyAttestationContext context)
    {
        var values = context.AttestationState?.Split('|', 3);

        if (values is not { Length: 3 } || context.CredentialJson != Credential(values[0]))
            return Task.FromResult(PasskeyAttestationResult.Fail(new PasskeyException("The passkey attestation is invalid.")));

        var passkey = new UserPasskeyInfo(
            CredentialId,
            PublicKey,
            DateTimeOffset.UtcNow,
            0,
            null,
            true,
            false,
            false,
            [],
            []);
        var user = new PasskeyUserEntity
        {
            Id = values[1],
            Name = values[2],
            DisplayName = values[2]
        };

        return Task.FromResult(PasskeyAttestationResult.Success(passkey, user));
    }

    public async Task<PasskeyAssertionResult<XpenseUser>> PerformAssertionAsync(PasskeyAssertionContext context)
    {
        if (context.AssertionState is null ||
            !TryGetCredentialId(context.CredentialJson, context.AssertionState, out var credentialId))
            return PasskeyAssertionResult.Fail<XpenseUser>(new PasskeyException("The passkey assertion is invalid."));

        var user = await userManager.FindByPasskeyIdAsync(credentialId);
        if (user is null)
            return PasskeyAssertionResult.Fail<XpenseUser>(new PasskeyException("The passkey assertion is invalid."));

        var passkey = await userManager.GetPasskeyAsync(user, credentialId);

        return passkey is null
            ? PasskeyAssertionResult.Fail<XpenseUser>(new PasskeyException("The passkey assertion is invalid."))
            : PasskeyAssertionResult.Success(passkey, user);
    }

    public static string CreateCredential(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        return Credential(options.RootElement.GetProperty("challenge").GetString()!);
    }

    private static string Credential(string challenge) => "software-authenticator-credential:" + challenge;

    public static string CreateAssertion(string optionsJson, byte[]? credentialId = null)
    {
        using var options = JsonDocument.Parse(optionsJson);
        return Assertion(options.RootElement.GetProperty("challenge").GetString()!, credentialId ?? CredentialId);
    }

    public static UserPasskeyInfo Passkey(byte[] credentialId) => new(
        credentialId,
        PublicKey,
        DateTimeOffset.UtcNow,
        0,
        null,
        true,
        false,
        false,
        [],
        []);

    private static string Assertion(string challenge, byte[] credentialId) =>
        "software-authenticator-assertion:" + challenge + ":" + Convert.ToBase64String(credentialId);

    private static bool TryGetCredentialId(string? credentialJson, string challenge, out byte[] credentialId)
    {
        credentialId = [];
        const string prefix = "software-authenticator-assertion:";

        if (credentialJson is null || !credentialJson.StartsWith(prefix + challenge + ":", StringComparison.Ordinal))
            return false;

        try
        {
            credentialId = Convert.FromBase64String(credentialJson[(prefix.Length + challenge.Length + 1)..]);
            return credentialId.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
