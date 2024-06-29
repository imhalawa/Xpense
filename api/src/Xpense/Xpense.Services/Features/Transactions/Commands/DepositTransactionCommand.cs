namespace Xpense.Services.Features.Transactions.Commands;

public class DepositTransactionCommand(decimal amount, string reason, string toAccount,int category, int[]? tags = null)
{
    public decimal Amount { get; set; } = amount;
    public string Reason { get; set; } = reason;
    public string ToAccount { get; set; } = toAccount;
    public int Category { get; set; } = category;
    public int[]? Tags { get; set; } = tags;
}