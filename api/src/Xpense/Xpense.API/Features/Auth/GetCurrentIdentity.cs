using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class GetCurrentIdentity : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/auth/me", Handle)
            .WithName(nameof(GetCurrentIdentity))
            .RequireAuthorization();

    private static async Task<Results<Ok<CurrentIdentityResponse>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        var wrappers = await dbContext.VaultWrappers
            .AsNoTracking()
            .Where(wrapper => wrapper.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var groups = await (
            from membership in dbContext.GroupMemberships.AsNoTracking()
            join groupEntity in dbContext.Groups.AsNoTracking() on membership.GroupId equals groupEntity.Id
            where membership.UserId == user.Id &&
                membership.State == MembershipState.Active &&
                !groupEntity.IsDeleted
            orderby groupEntity.CreatedAt, groupEntity.Id
            select new GroupSummaryResponse(
                groupEntity.Id,
                membership.Role,
                Convert.ToBase64String(groupEntity.NameCiphertext),
                Convert.ToBase64String(groupEntity.NameNonce),
                groupEntity.ProtocolVersion,
                membership.GroupKeyEnvelope != null))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(CurrentIdentityResponse.Of(user, wrappers, groups: groups));
    }
}
