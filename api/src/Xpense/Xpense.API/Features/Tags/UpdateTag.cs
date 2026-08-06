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
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Tags;

public sealed class UpdateTag : IEndpoint
{
    public sealed record Request(string Label, string BgColorHex, string FgColorHex);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Label)
                .NotEmpty().WithMessage("The label is required.")
                .MaximumLength(100);

            RuleFor(request => request.BgColorHex).HexColour("background colour");
            RuleFor(request => request.FgColorHex).HexColour("foreground colour");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/tags/{id:int}", Handle).WithName(nameof(UpdateTag)).Validated();

    private static async Task<Ok<TagResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken)
                  ?? throw new TagNotFoundException(id);

        tag.Label = request.Label;
        tag.BgColorHex = TagColour.Normalise(request.BgColorHex);
        tag.FgColorHex = TagColour.Normalise(request.FgColorHex);
        tag.Touch();

        if (await dbContext.SaveChangesAsync(cancellationToken) < 1)
            throw new TagUpdateFailedException(id);

        return TypedResults.Ok(TagResponse.Of(tag));
    }
}
