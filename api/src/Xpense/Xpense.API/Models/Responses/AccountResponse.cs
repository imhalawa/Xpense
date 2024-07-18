using System;

namespace Xpense.API.Models.Responses;

public class AccountResponse(
    int id,
    string accountNumber,
    string label,
    decimal balance,
    bool isDefault,
    long? createdOn, long? lastUpdated)
{
    public int Id { get; set; } = id;
    public string AccountNumber { get; set; } = accountNumber;
    public string Label { get; set; } = label;
    public decimal Balance { get; set; } = balance;
    public bool IsDefault { get; set; } = isDefault;
    public long? CreatedOn { get; set; } = createdOn;
    public long? LastUpdated { get; set; } = lastUpdated;


    public static AccountResponse Of(Services.Entities.Account account)
    {
        var createdOn = new DateTimeOffset(account.CreatedOn).ToUnixTimeSeconds();
        long? lastUpdated = account.LastUpdated.HasValue
            ? new DateTimeOffset(account.LastUpdated.Value).ToUnixTimeSeconds()
            : null;

        return new
        (
            account.Id,
            account.AccountNumber,
            account.Name,
            account.Balance,
            account.IsDefaultAccount,
            createdOn,
            lastUpdated
        );
    }
}