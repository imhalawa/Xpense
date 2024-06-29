namespace Xpense.API.Models.Responses;

public class CreateAccountResponse(int id, string name, decimal balance, string number)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public decimal Balance { get; set; } = balance;
    public string Number { get; set; } = number;

    public static CreateAccountResponse Of(Services.Entities.Account account) => new(account.Id, account.Name, account.Balance, account.AccountNumber);
}