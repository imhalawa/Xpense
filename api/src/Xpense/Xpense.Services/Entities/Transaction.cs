using Xpense.Services.Enums;

namespace Xpense.Services.Entities
{
    public class Transaction : BaseEntity
    {
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public TransactionType TransactionType { get; set; }

        public int? FromAccountId { get; set; }
        public Account? FromAccount { get; set; }

        public int ToAccountId { get; set; }
        public Account? ToAccount { get; set; }

        public int CategoryId { get; set; }
        public required Category Category { get; set; }

        public virtual ICollection<Tag>? Tags { get; set; }
    }
}