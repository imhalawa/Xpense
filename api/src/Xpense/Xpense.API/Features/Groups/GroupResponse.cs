using System;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Groups;

/// <summary>
/// Describes an encrypted group through the caller's active membership.
/// </summary>
public sealed record GroupResponse(
    Guid Id,
    Guid OwnerUserId,
    MembershipRole Role,
    string NameCiphertext,
    string NameNonce,
    int ProtocolVersion,
    string? GroupKeyEnvelope,
    int EnvelopeProtocolVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static GroupResponse Of(Group group, GroupMembership membership) => new(
        group.Id,
        group.OwnerUserId,
        membership.Role,
        Convert.ToBase64String(group.NameCiphertext),
        Convert.ToBase64String(group.NameNonce),
        group.ProtocolVersion,
        membership.GroupKeyEnvelope is null
            ? null
            : Convert.ToBase64String(membership.GroupKeyEnvelope),
        membership.EnvelopeProtocolVersion,
        group.CreatedAt,
        group.UpdatedAt);
}
