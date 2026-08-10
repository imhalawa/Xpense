using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Infrastructure.LegacyClaim;

public sealed class LegacyClaimWriteEndpointFilter(IOptions<LegacyClaimOptions> options)
    : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (options.Value.Enabled &&
            (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<LegacyFinancialWriteMetadata>() is not null ||
             context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<LegacyClaimSourceWriteMetadata>() is not null))
        {
            throw new LegacyClaimInProgressException();
        }

        return next(context);
    }
}

public sealed class LegacyFinancialWriteMetadata
{
    public static readonly LegacyFinancialWriteMetadata Instance = new();

    private LegacyFinancialWriteMetadata()
    {
    }
}

public sealed class LegacyClaimSourceWriteMetadata
{
    public static readonly LegacyClaimSourceWriteMetadata Instance = new();

    private LegacyClaimSourceWriteMetadata()
    {
    }
}

public static class LegacyFinancialWriteEndpointExtensions
{
    public static TBuilder BlocksDuringLegacyClaim<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(LegacyFinancialWriteMetadata.Instance);
        return builder;
    }

    public static TBuilder WritesLegacyClaimSource<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(LegacyClaimSourceWriteMetadata.Instance);
        return builder;
    }
}
