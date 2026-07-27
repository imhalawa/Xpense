using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace Xpense.API.Infrastructure;

public static class EndpointExtensions
{
    /// <summary>
    /// Finds every <see cref="IEndpoint"/> in this assembly and invokes its static Map method.
    /// This replaces the 31 hand-written AddScoped registrations the use-case layer needed:
    /// adding a slice is now creating one file, with no registration step to forget.
    /// </summary>
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
