using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Categories;

public sealed class CreateCategory : IEndpoint
{
    public sealed record Request(string Name, int PriorityId);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The name is required.")
                .MaximumLength(200);

            RuleFor(request => request.PriorityId)
                .GreaterThan(0).WithMessage("The priorityId must reference an existing priority.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/categories", Handle).WithName(nameof(CreateCategory)).Validated();

    private static async Task<Created<CategoryResponse>> Handle(
        Request request,
        XpenseDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var priority = await db.Priorities.FirstOrDefaultAsync(p => p.Id == request.PriorityId, ct)
                       ?? throw new PriorityNotFoundException(request.PriorityId);

        var category = new Category
        {
            Label = request.Name,
            Priority = priority,
            CreatedOn = DateTime.UtcNow
        };

        db.Categories.Add(category);

        if (await db.SaveChangesAsync(ct) < 1)
            throw new CategoryCreationFailedException(request.Name);

        return TypedResults.Created(
            http.ResourceUri($"/api/v1/categories/{category.Id}"),
            CategoryResponse.Of(category));
    }
}
