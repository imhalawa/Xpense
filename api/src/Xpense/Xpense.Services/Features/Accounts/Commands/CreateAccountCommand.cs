namespace Xpense.Services.Features.Accounts.Commands
{
    public record CreateAccountCommand(string Name, decimal Balance);
}