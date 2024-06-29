namespace Xpense.Services.Models.Response.Account;

public class GetAccountResponse(
    int id,
    string accountNumber,
    string name,
    decimal balance,
    bool isDefault,
    DateTime createdAt,
    DateTime? lastModifiedOn)
{
    public int Id { get; set; } = id;
    public string AccountNumber { get; set; } = accountNumber;
    public string Name { get; set; } = name;
    public decimal Balance { get; set; } = balance;
    public bool IsDefault { get; set; } = isDefault;
    public DateTime CreatedAt { get; set; } = createdAt;
    public DateTime? LastModifiedOn { get; set; } = lastModifiedOn;

    public static GetAccountResponse Of(Services.Entities.Account account) => new
    (
        account.Id,
        account.AccountNumber,
        account.Name,
        account.Balance,
        account.IsDefaultAccount,
        account.CreatedOn,
        account.LastUpdated
    );
}