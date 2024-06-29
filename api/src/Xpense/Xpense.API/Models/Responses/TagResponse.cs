using System;
using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public class TagResponse(
    int id,
    string name,
    string bgColorHex,
    string fgColorHex,
    DateTime createdAt,
    DateTime? lastModifiedOn)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string BgColorHex { get; set; } = bgColorHex;
    public string FgColorHex { get; set; } = fgColorHex;
    public DateTime CreatedAt { get; set; } = createdAt;
    public DateTime? LastModifiedOn { get; set; } = lastModifiedOn;

    public static TagResponse Of(Tag tag) =>
        new(tag.Id, tag.Name, tag.BgColorHex, tag.FgColorHex, tag.CreatedOn, tag.LastUpdated);
}