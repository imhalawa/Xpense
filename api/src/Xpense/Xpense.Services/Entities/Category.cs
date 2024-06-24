using Xpense.Services.Enums;

namespace Xpense.Services.Entities;

public class Category : BaseEntity
{
    public required string Name { get; set; }
    public Priority Priority { get; set; }
    public virtual required ICollection<Transaction> Transactions { get; set; }
}