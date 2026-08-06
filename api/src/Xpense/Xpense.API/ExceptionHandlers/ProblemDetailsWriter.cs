using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Xpense.API.ExceptionHandlers;

internal static class ProblemDetailsWriter
{
    public static async ValueTask<bool> Write(
        IProblemDetailsService problemDetailsService,
        HttpContext context,
        System.Exception exception,
        int statusCode,
        string title,
        string detail)
    {
        context.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = $"{context.Request.Method} {context.Request.Path}"
            }
        });
    }

    public static string ToCamelCasePath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return propertyPath;

        var segments = propertyPath.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
                segments[i] = char.ToLowerInvariant(segment[0]) + segment.Substring(1);
        }

        return string.Join(".", segments);
    }
}
