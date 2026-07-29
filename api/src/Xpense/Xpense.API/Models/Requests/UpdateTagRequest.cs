using Xpense.Services.Features.Tags.Commands;

namespace Xpense.API.Models.Requests;

public class UpdateTagRequest
{
    public int Id { get; set; }

    /// <summary>
    /// Named to match CreateTagRequest and TagResponse. Previously "name" on update only,
    /// which made the same field two different names across the tag contract.
    /// </summary>
    public string Label { get; set; }

    public string BgColorHex { get; set; }
    public string FgColorHex { get; set; }

    public UpdateTagCommand ToCommand() => new(Id, Label, BgColorHex, FgColorHex);
}
