using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Entities;

public class Tag : BaseEntity, IOptionEntity
{
    public required string Label { get; set; }
    public string? BgColorHex { get; set; }
    public string? FgColorHex { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; }
}