using Xpense.Services.Entities;

namespace Xpense.API.Models.Responses;

public class DepositTransactionResponse(
    decimal amount,
    string accountName,
    string accountNumber)
{
    public decimal Amount { get; set; } = amount;
    public string AccountName { get; set; } = accountName;
    public string AccountNumber { get; set; } = accountNumber;

    public static DepositTransactionResponse Of(Transaction transaction)
    {
        return new(
            transaction.Amount,
            transaction.ToAccount?.Name,
            transaction.ToAccount?.AccountNumber
        );
    }

    public override string ToString()
    {
        return $"The account {AccountName}:{AccountNumber} has been credited with {Amount}";
    }
}