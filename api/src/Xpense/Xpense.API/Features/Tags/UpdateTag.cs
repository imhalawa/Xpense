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

            RuleFor(request => request.BgColorHex).HexColour("bgColorHex");
            RuleFor(request => request.FgColorHex).HexColour("fgColorHex");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/v1/tags/{id:int}", Handle).WithName(nameof(UpdateTag)).Validated();

    private static async Task<Ok<TagResponse>> Handle(
        int id,
        Request request,
        XpenseDbContext db,
        CancellationToken ct)
    {
        // The old UpdateTagUseCase dereferenced the entity without a null check and threw
        // NullReferenceException for a missing id.
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)
                  ?? throw new TagNotFoundException(id);

        tag.Label = request.Label;
        tag.BgColorHex = TagColour.Normalise(request.BgColorHex);
        tag.FgColorHex = TagColour.Normalise(request.FgColorHex);
        tag.Touch();

        if (await db.SaveChangesAsync(ct) < 1)
            throw new TagUpdateFailedException(id);

        return TypedResults.Ok(TagResponse.Of(tag));
    }
}
