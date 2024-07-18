using System;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public class TagResponse(
    int id,
    string label,
    string bgColorHex,
    string fgColorHex,
    long? createdOn,
    long? lastUpdated)
{
    public int Id { get; set; } = id;
    public string Label { get; set; } = label;
    public bool Create { get; set; } = false;
    public string? BgColorHex { get; set; } = bgColorHex;
    public string? FgColorHex { get; set; } = fgColorHex;
    public long? CreatedOn { get; set; } = createdOn;
    public long? LastUpdated { get; set; } = lastUpdated;

    public static TagResponse Of(Tag tag)
    {
        var createdOn = new DateTimeOffset(tag.CreatedOn).ToUnixTimeSeconds();
        long? lastUpdated = tag.LastUpdated.HasValue
            ? new DateTimeOffset(tag.LastUpdated.Value).ToUnixTimeSeconds()
            : null;
        return new(tag.Id, tag.Label, tag.BgColorHex, tag.FgColorHex, createdOn, lastUpdated);
    }
}