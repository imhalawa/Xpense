using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public class CreateTagResponse(int id, string name, string bgColorHex, string fgColorHex)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string BgColorHex { get; set; } = bgColorHex;
    public string FgColorHex { get; set; } = fgColorHex;

    public static CreateTagResponse Of(Tag tag) => new(tag.Id, tag.Label, tag.BgColorHex, tag.FgColorHex);
}