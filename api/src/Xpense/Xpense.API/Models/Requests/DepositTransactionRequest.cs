using Xpense.Services.Features.Transactions;
using Xpense.Services.Features.Transactions.Commands;

namespace Xpense.API.Models.Requests;

public class DepositTransactionRequest
{
    public decimal Amount { get; set; }
    public string Reason { get; set; }
    public string ToAccount { get; set; }
    public int Category { get; set; }
    public int[]? Tags { get; set; }

    public DepositTransactionCommand ToCommand() => new(Amount, Reason, ToAccount, Category, Tags);
}