using Xpense.Services.UseCases.Account;

namespace Xpense.Services.Models.Requests.Account;

public class UpdateAccountRequest(string number, string name, bool isDefault)
{
    public string Number { get; set; } = number;
    public string Name { get; set; } = name;
    public bool IsDefault { get; set; } = isDefault;

    public UpdateAccountCommand ToCommand() => new(Number, Name, IsDefault);
}