using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

    public Task<PasskeyRequestOptionsResult> MakeRequestOptionsAsync(XpenseUser? user, HttpContext httpContext) =>
        throw new NotSupportedException();

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

    public Task<PasskeyAssertionResult<XpenseUser>> PerformAssertionAsync(PasskeyAssertionContext context) =>
        throw new NotSupportedException();

    public static string CreateCredential(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        return Credential(options.RootElement.GetProperty("challenge").GetString()!);
    }

    private static string Credential(string challenge) => "software-authenticator-credential:" + challenge;
}
