using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Entities
{
    public class Merchant : BaseEntity, IOptionEntity
    {
        public required string Label { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
    }
}
