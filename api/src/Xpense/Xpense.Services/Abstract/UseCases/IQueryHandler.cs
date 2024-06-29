namespace Xpense.Services.Abstract.UseCases;

public interface IQueryHandler<TResult>
{
    public Task<TResult> Execute();
}