using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;
using Xpense.API.Models;

namespace Xpense.API.Middlewares;

public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
            HandleException(context, ex);
        }
    }

    private static void HandleException(HttpContext context, Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        context.Response.WriteAsync(Serialize(ex));
    }

    private static string Serialize(Exception ex)
    {
        var response =
            Response.Problem(
                ErrorDetails.Of("Internal Server Error, please check the logs or contact the administrator!"));
        return JsonSerializer.Serialize(response);
    }
}