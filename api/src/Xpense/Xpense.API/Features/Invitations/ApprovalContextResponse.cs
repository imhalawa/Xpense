using System;

namespace Xpense.API.Features.Invitations;

/// <summary>
/// Supplies the public encryption material required to approve an awaiting invitation.
/// </summary>
public sealed record ApprovalContextResponse(
    Guid InvitationId,
    Guid AcceptedUserId,
    string PublicKey,
    int ProtocolVersion);
