using Xpense.Services.Features.Tags.Commands;

namespace Xpense.API.Models.Requests;

public class UpdateTagRequest
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string BgColorHex { get; set; }
    public string FgColorHex { get; set; }

    public UpdateTagCommand ToCommand() => new(Id, Name, BgColorHex, FgColorHex);
}