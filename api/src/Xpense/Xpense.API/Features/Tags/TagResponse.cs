using System;
using Xpense.Services.Entities;

namespace Xpense.API.Features.Tags;

/// <summary>
/// Shared by all five tag slices: a tag looks the same however you fetched it.
/// </summary>
public sealed record TagResponse(
    int Id,
    string Label,
    string BgColorHex,
    string FgColorHex,
    long? CreatedOn,
    long? LastUpdated)
{
    public static TagResponse Of(Tag tag) => new(
        tag.Id,
        tag.Label,
        tag.BgColorHex,
        tag.FgColorHex,
        new DateTimeOffset(tag.CreatedOn).ToUnixTimeSeconds(),
        tag.LastUpdated.HasValue
            ? new DateTimeOffset(tag.LastUpdated.Value).ToUnixTimeSeconds()
            : null);
}
