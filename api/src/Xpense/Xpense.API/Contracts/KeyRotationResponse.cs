namespace Xpense.API.Contracts;

/// <summary>
/// Indicates whether revocation requires the remaining clients to rotate the group key.
/// </summary>
public sealed record KeyRotationResponse(bool KeyRotationRequired);
