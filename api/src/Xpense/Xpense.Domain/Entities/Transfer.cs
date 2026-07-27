using Xpense.Domain.Abstract.Entities;
using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class Transfer : BaseEntity
{
    public long Amount { get; set; }
    public Currency Currency { get; set; }
    public string? Reason { get; set; }

    public int SourceAccountId { get; set; }
    public required Account SourceAccount { get; set; }

    public int DestinationAccountId { get; set; }
    public required Account DestinationAccount { get; set; }

    public required ICollection<TransferLeg> Legs { get; set; }
}
