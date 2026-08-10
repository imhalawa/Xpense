using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Persistence;

namespace Xpense.API.Features.Claim;

public sealed class GetLegacyClaimStatus : IEndpoint
{
    /// <summary>
    /// Describes whether the browser must use legacy, claim maintenance, or encrypted data.
    /// </summary>
    public sealed record Response(string Mode);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/claim/status", Handle)
            .WithName(nameof(GetLegacyClaimStatus))
            .RequireAuthorization();

    private static async Task<Ok<Response>> Handle(
        XpenseDbContext dbContext,
        IOptions<LegacyClaimOptions> options,
        CancellationToken cancellationToken)
    {
        var completed = await dbContext.ClaimTokens
            .AsNoTracking()
            .AnyAsync(
                claimToken => claimToken.Purpose == ClaimToken.LegacyPurpose &&
                    claimToken.ConsumedAt != null,
                cancellationToken);
        var mode = options.Value.IsEncrypted || completed
            ? "encrypted"
            : options.Value.Enabled ? "claiming" : "legacy";
        return TypedResults.Ok(new Response(mode));
    }
}
