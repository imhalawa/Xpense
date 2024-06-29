namespace Xpense.Services.Features.Tags.Commands;

public class UpdateTagCommand(int id, string name, string bgColorHex, string fgColorHex)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string BgColorHex { get; set; } = bgColorHex;
    public string FgColorHex { get; set; } = fgColorHex;
}