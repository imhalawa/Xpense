using System;
using System.Collections.Generic;
using System.Linq;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Auth;

/// <summary>
/// Describes the signed-in identity and its available vault wrappers.
/// </summary>
public sealed record CurrentIdentityResponse(
    Guid Id,
    string Email,
    AccountState State,
    VaultWrapperKind[] AvailableVaultWrappers)
{
    public static CurrentIdentityResponse Of(XpenseUser user, IEnumerable<VaultWrapper> wrappers) =>
        new(user.Id, user.Email!, user.State, wrappers.Select(wrapper => wrapper.Kind).Distinct().ToArray());
}
