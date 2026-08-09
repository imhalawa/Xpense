using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Xpense.API.Infrastructure.Authentication;

public sealed class AntiforgeryEndpointFilter(
    IAntiforgery antiforgery,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        var sessionCookieName = cookieOptions.Get(IdentityConstants.ApplicationScheme).Cookie.Name;

        if (!IsMutation(request.Method) ||
            sessionCookieName is null ||
            !request.Cookies.ContainsKey(sessionCookieName))
            return await next(context);

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest();
        }

        return await next(context);
    }

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);
}
