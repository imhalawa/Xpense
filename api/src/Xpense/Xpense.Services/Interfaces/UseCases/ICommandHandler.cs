namespace Xpense.Services.Interfaces.UseCases;

public interface ICommandHandler<TParam>
{
    public Task Handle(TParam accountNumber);
}