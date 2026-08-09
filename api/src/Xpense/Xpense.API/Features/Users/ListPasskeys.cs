using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Users;

public sealed class ListPasskeys : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/users/me/passkeys", Handle)
            .WithName(nameof(ListPasskeys))
            .RequireAuthorization();

    private static async Task<Results<Ok<PasskeyResponse[]>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        var passkeys = await userManager.GetPasskeysAsync(user);
        var wrappers = await dbContext.VaultWrappers
            .AsNoTracking()
            .Where(wrapper => wrapper.UserId == user.Id && wrapper.Kind == VaultWrapperKind.Passkey)
            .ToListAsync(cancellationToken);
        var response = passkeys
            .Select(passkey =>
            {
                var wrapper = wrappers.SingleOrDefault(item =>
                    item.CredentialId is not null && item.CredentialId.SequenceEqual(passkey.CredentialId));
                return new PasskeyResponse(
                    WebEncoders.Base64UrlEncode(passkey.CredentialId),
                    wrapper?.Label,
                    wrapper?.LastUsedAt);
            })
            .OrderBy(passkey => passkey.Label, System.StringComparer.Ordinal)
            .ThenBy(passkey => passkey.CredentialId, System.StringComparer.Ordinal)
            .ToArray();

        return TypedResults.Ok(response);
    }
}
