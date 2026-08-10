using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Xpense.API.Infrastructure;

namespace Xpense.API.Features.Auth;

public sealed class GetAntiforgeryToken : IEndpoint
{
    /// <summary>
    /// Carries the request token paired with the antiforgery cookie.
    /// </summary>
    public sealed record Response(string RequestToken);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/auth/antiforgery", Handle)
            .WithName(nameof(GetAntiforgeryToken))
            .AllowAnonymous();

    private static Ok<Response> Handle(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return TypedResults.Ok(new Response(tokens.RequestToken!));
    }
}
