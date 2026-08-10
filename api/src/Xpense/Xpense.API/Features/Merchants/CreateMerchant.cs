using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.LegacyClaim;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Merchants;

public sealed class CreateMerchant : IEndpoint
{
    public sealed record Request(string Label);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Label)
                .NotEmpty().WithMessage("The label is required.")
                .MaximumLength(100);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/merchants", Handle).WithName(nameof(CreateMerchant)).Validated().BlocksDuringLegacyClaim();

    private static async Task<Created<MerchantResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var merchant = new Merchant
        {
            Label = request.Label.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Merchants.Add(merchant);

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new MerchantCreationFailedException(request.Label);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/merchants/{merchant.Id}"),
            MerchantResponse.Of(merchant));
    }
}
