namespace Xpense.Services.Features.Accounts.Commands;

public class UpdateAccountCommand(int id, string name, bool isDefault)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public bool IsDefault { get; set; } = isDefault;
}
