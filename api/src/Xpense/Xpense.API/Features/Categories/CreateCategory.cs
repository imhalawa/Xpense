using System;
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
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Categories;

public sealed class CreateCategory : IEndpoint
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
        app.MapPost("/api/v1/categories", Handle).WithName(nameof(CreateCategory)).Validated();

    private static async Task<Created<CategoryResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var priority = await dbContext.Priorities.FirstOrDefaultAsync(priority => priority.Id == request.PriorityId, cancellationToken)
                       ?? throw new PriorityNotFoundException(request.PriorityId);

        var category = new Category
        {
            Label = request.Label,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Categories.Add(category);

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new CategoryCreationFailedException(request.Label);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/categories/{category.Id}"),
            CategoryResponse.Of(category));
    }
}
