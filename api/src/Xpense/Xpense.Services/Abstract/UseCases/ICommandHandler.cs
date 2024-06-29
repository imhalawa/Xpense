namespace Xpense.Services.Abstract.UseCases;

public interface ICommandHandler<TParam>
{
    public Task Handle(TParam accountNumber);
}