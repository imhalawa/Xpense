using Xpense.Services.Features.Tags.Commands;

namespace Xpense.API.Models.Requests;

public class CreateTagRequest
{
    public string Name { get; set; }
    public string BgColorHex { get; set; }
    public string FgColorHex { get; set; }

    public CreateTagCommand ToCommand() => new(Name, BgColorHex, FgColorHex);
}