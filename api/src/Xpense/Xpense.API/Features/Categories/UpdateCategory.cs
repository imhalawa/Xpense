using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Categories;

public sealed class UpdateCategory : IEndpoint
{
    public sealed record Request(string Label, int PriorityId);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Label)
                .NotEmpty().WithMessage("The label is required.")
                .MaximumLength(200);

            RuleFor(request => request.PriorityId)
                .GreaterThan(0).WithMessage("The priority must be a valid selection.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/categories/{id:int}", Handle).WithName(nameof(UpdateCategory)).Validated();

    private static async Task<Ok<CategoryResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var priority = await dbContext.Priorities.FirstOrDefaultAsync(priority => priority.Id == request.PriorityId, cancellationToken)
                       ?? throw new PriorityNotFoundException(request.PriorityId);

        var category = await dbContext.Categories.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                       ?? throw new CategoryNotFoundException(id);

        category.Label = request.Label;
        category.Priority = priority;
        category.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new CategoryUpdateFailedException(id);

        return TypedResults.Ok(CategoryResponse.Of(category));
    }
}
