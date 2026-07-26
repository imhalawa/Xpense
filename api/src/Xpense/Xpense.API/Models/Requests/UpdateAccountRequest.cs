using Xpense.Services.Features.Accounts.Commands;

namespace Xpense.API.Models.Requests;

public class UpdateAccountRequest(string name, bool isDefault)
{
    public string Name { get; set; } = name;
    public bool IsDefault { get; set; } = isDefault;

    public UpdateAccountCommand ToCommand(int id) => new(id, Name, IsDefault);
}
