namespace Xpense.Services.Interfaces.UseCases;

public interface IQueryHandler<TResult>
{
    public Task<TResult> Execute();
}