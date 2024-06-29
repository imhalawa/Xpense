namespace Xpense.Services.Abstract.UseCases;

public interface IQueryParamHandler<in TParam, TResult>
{
    public Task<TResult> Execute(TParam query, CancellationToken cancellationToken = default);
}
