namespace Xpense.Services.Features.Transactions.Commands;

public class WithdrawTransactionCommand(decimal amount, string reason, string fromAccount,int category, int[]? tags = null)
{
    public decimal Amount { get; set; } = amount;
    public string Reason { get; set; } = reason;
    public string FromAccount { get; set; } = fromAccount;
    public int Category { get; set; } = category;
    public int[]? Tags { get; set; } = tags;
}