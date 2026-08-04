using Xpense.Domain.Entities;

namespace Xpense.Domain.Entities
{
    public class Merchant : BaseEntity, IOptionEntity
    {
        public required string Label { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
