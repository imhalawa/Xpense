using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Xpense.API.Infrastructure;

public static class HttpContextExtensions
{
    public static string ResourceUri(this HttpContext httpContext, string path) =>
        UriHelper.BuildAbsolute(httpContext.Request.Scheme, httpContext.Request.Host, path: path);
}
