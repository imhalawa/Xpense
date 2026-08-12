using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Merchants;

public sealed class UpdateMerchant : IEndpoint
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
        app.MapPut("/api/v1/merchants/{id:int}", Handle).WithName(nameof(UpdateMerchant)).Validated();

    private static async Task<Ok<MerchantResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var merchant = await dbContext.Merchants.SingleOrDefaultAsync(
                           merchant => merchant.Id == id,
                           cancellationToken)
                       ?? throw new MerchantNotFoundException(id);

        merchant.Label = request.Label.Trim();
        merchant.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new MerchantUpdateFailedException(id);

        return TypedResults.Ok(MerchantResponse.Of(merchant));
    }
}
