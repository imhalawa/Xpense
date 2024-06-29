namespace Xpense.Services.Models.Response.Account;

public class CreateAccountResponse(int id, string name, decimal balance)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public decimal Balance { get; set; } = balance;

    public static CreateAccountResponse Of(Services.Entities.Account account) => new(account.Id, account.Name, account.Balance);
}