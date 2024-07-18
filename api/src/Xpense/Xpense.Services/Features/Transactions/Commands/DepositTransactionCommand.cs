using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.Services.Features.Transactions.Commands
{
    public class DepositTransactionCommand(Money amount, string accountNumber, int categoryId, Merchant merchant, Tag[]? tags = null, long? createdOn = null)
    {
        public Money Amount { get; set; } = amount;
        public long? CreatedOn { get; set; } = createdOn;
        public string AccountNumber { get; set; } = accountNumber;
        public int CategoryId { get; set; } = categoryId;
        public Merchant Merchant { get; set; } = merchant;
        public Tag[]? Tags { get; set; } = tags;
    }
}