namespace Xpense.Services.Entities;

public class Category : BaseEntity
{
    public required string Label { get; set; }
    public required int PriorityId { get; set; }
    public required Priority Priority { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
}