using Xpense.Services.Abstract.Entities;
using Xpense.Services.Enums;

namespace Xpense.Services.Entities;

public class TransferLeg : BaseEntity
{
    public int TransferId { get; set; }
    public required Transfer Transfer { get; set; }

    public int AccountId { get; set; }
    public required Account Account { get; set; }

    public TransferLegDirection Direction { get; set; }
    public long Amount { get; set; }
    public Currency Currency { get; set; }
}
