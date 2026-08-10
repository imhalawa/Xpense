using System;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Groups;

/// <summary>
/// Describes one server-visible resource grant without exposing encrypted key material.
/// </summary>
public sealed record ResourceGrantResponse(
    Guid Id,
    Guid GroupId,
    SharedResourceType ResourceType,
    Guid ResourceId,
    GrantPermission Permission,
    GrantState State,
    Guid GrantedByUserId,
    Guid? KeyEnvelopeReference,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? RevokedAt)
{
    public static ResourceGrantResponse Of(ResourceGrant grant) => new(
        grant.Id,
        grant.GroupId,
        grant.ResourceType,
        grant.ResourceId,
        grant.Permission,
        grant.State,
        grant.GrantedByUserId,
        grant.KeyEnvelopeReference,
        grant.CreatedAt,
        grant.UpdatedAt,
        grant.RevokedAt);
}
