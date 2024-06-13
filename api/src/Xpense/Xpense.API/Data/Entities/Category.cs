using System.Collections.Generic;
using Xpense.API.Data.Enums;

namespace Xpense.API.Data.Models;

public class Category:BaseEntity
{
    public string Name { get; set; }
    public Priority Priority { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
}