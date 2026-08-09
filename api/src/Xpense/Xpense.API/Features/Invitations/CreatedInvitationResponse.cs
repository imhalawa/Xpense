using System;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Invitations;

/// <summary>
/// Returns the one-time bearer link for a newly created invitation.
/// </summary>
public sealed record CreatedInvitationResponse(
    Guid Id,
    Guid GroupId,
    string? TargetEmail,
    InvitationState State,
    DateTime ExpiresAt,
    bool HasKeyEnvelope,
    string InvitationLink);
