using Xpense.Domain.Entities;

namespace Xpense.Domain.Entities;

public class Category : BaseEntity
{
    public required string Label { get; set; }
    public int PriorityId { get; set; }
    public required Priority Priority { get; set; }
    public virtual ICollection<Transaction>? Transactions { get; set; }
}