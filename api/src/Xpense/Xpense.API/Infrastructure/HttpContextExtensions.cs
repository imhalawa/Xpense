using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Xpense.API.Infrastructure;

public static class HttpContextExtensions
{
    /// <summary>
    /// Builds an absolute URL for a created resource.
    /// <para>
    /// MVC's CreatedAtAction emitted an absolute Location header; minimal APIs'
    /// TypedResults.Created emits whatever string you hand it, so passing a path yields a
    /// relative Location. Both are legal per RFC 9110, but the v1 contract is absolute, so
    /// every create slice goes through here rather than each one deciding.
    /// </para>
    /// </summary>
    public static string ResourceUri(this HttpContext http, string path) =>
        UriHelper.BuildAbsolute(http.Request.Scheme, http.Request.Host, path: path);
}
