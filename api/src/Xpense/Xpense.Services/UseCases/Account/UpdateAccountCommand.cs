namespace Xpense.Services.UseCases.Account;

public class UpdateAccountCommand(string number, string name, bool isDefault)
{
    public string Number { get; set; } = number;
    public string Name { get; set; } = name;
    public bool IsDefault { get; set; } = isDefault;
}