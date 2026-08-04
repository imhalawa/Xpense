using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Xpense.API.ExceptionHandlers;

/// <summary>
/// Shared plumbing for the exception handlers. Each handler decides the status, title and
/// detail; this writes the RFC 7807 payload so every error response has the same shape.
/// </summary>
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

    /// <summary>
    /// FluentValidation reports PascalCase property paths ("Amount.Cents"); the JSON contract
    /// is camelCase ("amount.cents"). Convert each dotted segment so the error keys line up
    /// with the field names the client actually sent.
    /// </summary>
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
