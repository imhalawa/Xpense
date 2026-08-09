using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Auth;

public sealed class SignOutSession : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/logout", Handle)
            .WithName(nameof(SignOutSession))
            .RequireAuthorization();

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        SignInManager<XpenseUser> signInManager)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        var stampUpdated = await userManager.UpdateSecurityStampAsync(user);

        if (!stampUpdated.Succeeded)
            return TypedResults.Unauthorized();

        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }
}
