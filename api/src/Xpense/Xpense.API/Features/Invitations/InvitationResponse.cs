using System;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Invitations;

/// <summary>
/// Describes an invitation without returning its bearer token, link, hash, or key envelope.
/// </summary>
public sealed record InvitationResponse(
    Guid Id,
    Guid GroupId,
    string? TargetEmail,
    InvitationState State,
    DateTime ExpiresAt,
    bool HasKeyEnvelope,
    DateTime CreatedAt,
    DateTime UpdatedAt);
