using Xpense.Services.Features.Transactions.Commands;
using Xpense.Services.Models;
using Xpense.Services.ValueObjects;

namespace Xpense.API.Models.Requests;

public class DepositTransactionRequest
{
    public Money Amount { get; set; }
    public long? CreatedOn { get; set; }
    public string AccountNumber { get; set; }
    public int CategoryId { get; set; }
    public Merchant Merchant { get; set; }
    public Tag[]? Tags { get; set; }

    public DepositTransactionCommand ToCommand() => new(Amount, AccountNumber, CategoryId, Merchant, Tags, CreatedOn);
}