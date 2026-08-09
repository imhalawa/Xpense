using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Xpense.API.Infrastructure.Authentication;

public sealed class OpenApiSecurityOperationFilter : IOperationFilter
{
    public const string SessionScheme = "sessionCookie";
    public const string AntiforgeryHeaderScheme = "antiforgeryHeader";
    public const string AntiforgeryCookieScheme = "antiforgeryCookie";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security = [];
            return;
        }

        var schemes = new List<string> { SessionScheme };
        if (IsMutation(context.ApiDescription.HttpMethod))
        {
            schemes.Add(AntiforgeryHeaderScheme);
            schemes.Add(AntiforgeryCookieScheme);
        }

        var requirement = new OpenApiSecurityRequirement();
        foreach (var scheme in schemes)
            requirement[new OpenApiSecuritySchemeReference(scheme, null!, null)] = [];
        operation.Security = [requirement];
    }

    private static bool IsMutation(string? method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);
}
