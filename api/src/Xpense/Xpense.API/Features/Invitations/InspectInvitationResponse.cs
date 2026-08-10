using System;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Invitations;

/// <summary>
/// Describes a usable invitation while withholding group ciphertext until the caller already holds its key.
/// </summary>
public sealed record InspectInvitationResponse(
    InvitationState State,
    bool RequiresApproval,
    string InviterEmail,
    string? NameCiphertext,
    string? NameNonce,
    int? ProtocolVersion);
