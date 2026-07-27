using Xpense.Services.Features.Tags.Commands;

namespace Xpense.API.Models.Requests;

public class CreateTagRequest
{
    public string Label { get; set; }
    public string BgColorHex { get; set; }
    public string FgColorHex { get; set; }

    public CreateTagCommand ToCommand() => new(Label, BgColorHex, FgColorHex);
}