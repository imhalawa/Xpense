using Xpense.API.Enums;

namespace Xpense.API.Data.Models;

public class Category:BaseEntity
{
    public string Name { get; set; }
    public int CategoryLevelId { get; set; }
    public CategoryLevel CategoryLevel { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
}