using System.Collections.Generic;

namespace Xpense.API.Data.Models;

public class Tag : BaseEntity
{
    public string Name { get; set; }
    public string BgColorHex { get; set; }
    public string FgColorHex { get; set; }

    public virtual ICollection<Account> Accounts { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; }
}