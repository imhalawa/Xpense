namespace Xpense.Services.Entities;

public class Tag : BaseEntity
{
    public required string Name { get; set; }
    public required string BgColorHex { get; set; }
    public required string FgColorHex { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; }
}