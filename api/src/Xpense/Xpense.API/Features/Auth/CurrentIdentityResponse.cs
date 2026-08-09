using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Auth;

/// <summary>
/// Describes the signed-in identity and its available vault wrappers.
/// </summary>
public sealed record CurrentIdentityResponse(
    Guid Id,
    string Email,
    AccountState State,
    VaultWrapperKind[] AvailableVaultWrappers,
    VaultWrapperResponse[] VaultWrappers,
    VaultState VaultState)
{
    public static CurrentIdentityResponse Of(
        XpenseUser user,
        IEnumerable<VaultWrapper> wrappers,
        byte[]? assertedCredentialId = null,
        Guid? unlockableWrapperId = null)
    {
        var wrapperValues = wrappers.ToArray();
        var wrapperResponses = wrapperValues.Select(VaultWrapperResponse.Of).ToArray();
        var vaultState = (assertedCredentialId is not null && wrapperValues.Any(wrapper =>
            wrapper.CredentialId is not null && wrapper.CredentialId.SequenceEqual(assertedCredentialId))) ||
            (unlockableWrapperId is not null && wrapperValues.Any(wrapper => wrapper.Id == unlockableWrapperId))
            ? VaultState.Unlockable
            : VaultState.Locked;

        return new(
            user.Id,
            user.Email!,
            user.State,
            wrapperResponses.Select(wrapper => wrapper.Kind).Distinct().ToArray(),
            wrapperResponses,
            vaultState);
    }
}

public sealed record VaultWrapperResponse(
    Guid Id,
    VaultWrapperKind Kind,
    string? CredentialId,
    string Salt,
    string Ciphertext,
    string Nonce,
    string? Parameters,
    int ProtocolVersion,
    string? Label,
    DateTime? LastUsedAt)
{
    public static VaultWrapperResponse Of(VaultWrapper wrapper) => new(
        wrapper.Id,
        wrapper.Kind,
        wrapper.CredentialId is null ? null : Convert.ToBase64String(wrapper.CredentialId),
        Convert.ToBase64String(wrapper.Salt),
        Convert.ToBase64String(wrapper.Ciphertext),
        Convert.ToBase64String(wrapper.Nonce),
        wrapper.Parameters,
        wrapper.ProtocolVersion,
        wrapper.Label,
        wrapper.LastUsedAt);
}

public enum VaultState
{
    Locked,
    Unlockable
}
