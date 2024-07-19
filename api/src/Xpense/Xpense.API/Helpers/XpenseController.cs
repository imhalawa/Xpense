using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Text.Json;
using Xpense.API.Models;
using Xpense.Services.Abstract.Entities;
using Xpense.Services.Models;

namespace Xpense.API.Helpers;

public class XpenseController : ControllerBase
{
    public OkObjectResult Ok<T>(T value)
    {
        var result = Response<T>.Ok(value);
        return base.Ok(result);
    }

    public OkObjectResult Ok<T, TK>(PaginatedResult<T> result, Func<T, TK> Of) where T : BaseEntity
    {
        var data = result.Data.Select(Of);
        var response = PaginatedResponse.Ok(data, result.Page, result.Pages, result.Size);
        return base.Ok(response);
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