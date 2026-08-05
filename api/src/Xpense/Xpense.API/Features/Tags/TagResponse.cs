using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Tags;

/// <summary>
/// Shared by all five tag slices: a tag looks the same however you fetched it.
/// </summary>
public sealed record TagResponse(
    int Id,
    string Label,
    string BgColorHex,
    string FgColorHex,
    string CreatedAt,
    string? UpdatedAt)
{
    public static TagResponse Of(Tag tag) => new(
        tag.Id,
        tag.Label,
        tag.BgColorHex,
        tag.FgColorHex,
        Timestamps.Iso(tag.CreatedAt),
        Timestamps.Iso(tag.UpdatedAt));
}
