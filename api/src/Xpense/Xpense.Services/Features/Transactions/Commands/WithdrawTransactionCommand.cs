using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.Services.Features.Transactions.Commands;

public class WithdrawTransactionCommand(Money amount, string fromAccountNumber, int category, Merchant merchant, Tag[]? tags = null, long? createdOn = null)
{
    public Money Amount { get; set; } = amount;
    public long? CreatedOn { get; set; } = createdOn;
    public string AccountNumber { get; set; } = fromAccountNumber;
    public int CategoryId { get; set; } = category;
    public Merchant Merchant { get; set; } = merchant;
    public Tag[]? Tags { get; set; } = tags;
}