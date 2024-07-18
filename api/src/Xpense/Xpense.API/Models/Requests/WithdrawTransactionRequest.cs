using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Requests;

public class WithdrawTransactionRequest
{
    public Money Amount { get; set; }
    public long? CreatedOn { get; set; }
    public string FromAccountNumber { get; set; }
    public int Category { get; set; }
    public Merchant Merchant { get; set; }
    public Tag[]? Tags { get; set; }

    public WithdrawTransactionCommand ToCommand() => new(Amount, FromAccountNumber, Category, Merchant, Tags, CreatedOn);
}