using Xpense.Services.Entities;

namespace Xpense.Services.Features.Tags.Commands;

public class CreateTagCommand(string name, string bgColorHex, string fgColorHex)
{
    public string Name { get; set; } = name;
    public string BgColorHex { get; set; } = bgColorHex;
    public string FgColorHex { get; set; } = fgColorHex;

    public Tag ToEntity() => new()
    {
        Name = Name,
        BgColorHex = BgColorHex,
        FgColorHex = FgColorHex
    };
}