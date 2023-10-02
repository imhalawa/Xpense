using Xpense.API.Enums;

namespace Xpense.API.Data.Models
{
    public class Transaction : BaseEntity
    {
        public decimal Amount { get; set; }
        public Account? FromAccount { get; set; }
        public Account? ToAccount { get; set; }
        public CurrencyRateAudit CurrencyRateAudit { get; set; }
        public Category Category { get; set; }
        public string? Reason { get; set; }
        public TransactionType TransactionType { get; set; }
        public virtual ICollection<Tag> Tags { get; set; }
    }
}
