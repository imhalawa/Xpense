using Microsoft.AspNetCore.Builder;
using Xpense.API.Middlewares;

namespace Xpense.API.Extensions.cs;

public static class MiddlewareExtensions
{
    public static void UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}