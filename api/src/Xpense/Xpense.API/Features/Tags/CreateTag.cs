using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Xpense.API.Infrastructure;
using Xpense.Persistence;
using Xpense.Domain.Entities;
using Xpense.Domain.Exceptions;

namespace Xpense.API.Features.Tags;

public sealed class CreateTag : IEndpoint
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
        app.MapPost("/api/v1/tags", Handle).WithName(nameof(CreateTag)).Validated();

    private static async Task<Created<TagResponse>> Handle(
        Request request,
        XpenseDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var tag = new Tag
        {
            Label = request.Label,
            BgColorHex = TagColour.Normalise(request.BgColorHex),
            FgColorHex = TagColour.Normalise(request.FgColorHex),
            CreatedAt = DateTime.UtcNow
        };

        db.Tags.Add(tag);

        if (await db.SaveChangesAsync(ct) < 1)
            throw new TagCreationFailedException(tag.Label);

        return TypedResults.Created(http.ResourceUri($"/api/v1/tags/{tag.Id}"), TagResponse.Of(tag));
    }
}
