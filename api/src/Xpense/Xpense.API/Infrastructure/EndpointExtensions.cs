using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace Xpense.API.Infrastructure;

public static class EndpointExtensions
{
    public static void MapEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = typeof(IEndpoint).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && type.IsAssignableTo(typeof(IEndpoint)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var endpoint in endpoints)
        {
            var map = endpoint.GetMethod(
                nameof(IEndpoint.Map),
                BindingFlags.Public | BindingFlags.Static);

            if (map is null)
                throw new InvalidOperationException(
                    $"{endpoint.FullName} implements IEndpoint but has no public static Map method.");

            map.Invoke(null, [app]);
        }
    }
}
