using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Xpense.API.Models;

namespace Xpense.API.Helpers;

public class XpenseController : ControllerBase
{
    public OkObjectResult Ok<T>(T value)
    {
        var result = Response<T>.Ok(value);
        return base.Ok(result);
    }

    public BadRequestObjectResult BadRequest<T>(T error)
    {
        var result = Response<T>.BadRequest(error);
        return base.BadRequest(result);
    }

    public NotFoundObjectResult NotFound<T>(T value)
    {
        var result = Response<T>.NotFound(value);
        return base.NotFound(result);
    }

    public ObjectResult Problem<T>(T detail, int? statusCode = null, string instance = null, string title = null,
        string type = null)
    {
        Response<T> result;
        if (statusCode.HasValue && statusCode.Value != 500)
        {
            result = Response<T>.Of(statusCode.Value, detail);
        }
        else
        {
            result = Response<T>.Problem(detail);
        }

        return base.Problem(JsonSerializer.Serialize(result), instance, statusCode, title, type);
    }
}