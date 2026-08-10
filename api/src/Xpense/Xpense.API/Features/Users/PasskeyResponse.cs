using System;

namespace Xpense.API.Features.Users;

/// <summary>
/// Describes a passkey registered for the signed-in user.
/// </summary>
public sealed record PasskeyResponse(
    string CredentialId,
    string? Label,
    DateTime? LastUsedAt);
