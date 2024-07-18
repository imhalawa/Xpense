using Xpense.Services.Abstract.Entities;
using Xpense.Services.Enums;

namespace Xpense.Services.Entities
{
    public class Transaction : BaseEntity
    {
        public long Amount { get; set; }
        public Currency Currency { get; set; }
        public TransactionType TransactionType { get; set; }

        public int? AccountId { get; set; }
        public Account? Account { get; set; }

        public int CategoryId { get; set; }
        public required Category Category { get; set; }

        public int MerchantId { get; set; }
        public required Merchant Merchant { get; set; }

        public virtual ICollection<Tag>? Tags { get; set; }
    }
}