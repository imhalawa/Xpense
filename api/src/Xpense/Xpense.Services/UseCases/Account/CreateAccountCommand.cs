namespace Xpense.Services.UseCases.Account
{
    public record CreateAccountCommand(string Name, decimal Balance);
}