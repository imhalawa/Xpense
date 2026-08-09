using System;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Groups;

/// <summary>
/// Describes an active group member without returning the group-key envelope.
/// </summary>
public sealed record GroupMemberResponse(
    Guid UserId,
    string Email,
    MembershipRole Role,
    bool HasKeyEnvelope);
