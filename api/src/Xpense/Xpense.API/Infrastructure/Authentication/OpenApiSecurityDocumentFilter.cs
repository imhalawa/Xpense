using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Xpense.API.Infrastructure.Authentication;

public sealed class OpenApiSecurityDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            var path = "/" + description.RelativePath!.Split('?')[0];
            if (!document.Paths.TryGetValue(path, out var pathItem)
                || !pathItem.Operations.TryGetValue(
                    new System.Net.Http.HttpMethod(description.HttpMethod!),
                    out var operation))
                continue;

            if (description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security = [];
                continue;
            }

            var requirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    OpenApiSecurityOperationFilter.SessionScheme,
                    document,
                    null)] = []
            };

            if (IsMutation(description.HttpMethod))
            {
                requirement[new OpenApiSecuritySchemeReference(
                    OpenApiSecurityOperationFilter.AntiforgeryHeaderScheme,
                    document,
                    null)] = [];
                requirement[new OpenApiSecuritySchemeReference(
                    OpenApiSecurityOperationFilter.AntiforgeryCookieScheme,
                    document,
                    null)] = [];
            }

            operation.Security = [requirement];
        }
    }

    private static bool IsMutation(string? method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);
}
