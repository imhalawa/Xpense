using System;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Invitations;

/// <summary>
/// Describes the caller's resulting group membership and only the encrypted material it can use.
/// </summary>
public sealed record AcceptInvitationResponse(
    Guid GroupId,
    MembershipState State,
    bool RequiresApproval,
    string? NameCiphertext,
    string? NameNonce,
    int? ProtocolVersion,
    string? GroupKeyEnvelope,
    int? EnvelopeProtocolVersion);
