using Xpense.Services.Features.Transactions.Commands;

namespace Xpense.API.Models.Requests;

public class WithdrawTransactionRequest
{
    public decimal Amount { get; set; }
    public string Reason { get; set; }
    public string FromAccount { get; set; }
    public int Category { get; set; }
    public int[] Tags { get; set; }

    public WithdrawTransactionCommand ToCommand() => new(Amount, Reason, FromAccount, Category, Tags);
}