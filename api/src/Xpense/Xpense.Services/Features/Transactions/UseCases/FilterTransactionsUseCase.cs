using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;
using Xpense.Services.Models;

namespace Xpense.Services.Features.Transactions.UseCases
{
    public class FilterTransactionsUseCase(ITransactionRepository repository) : IQueryParamHandler<FilterQuery, PaginatedResult<Transaction>>
    {
        public async Task<PaginatedResult<Transaction>> Execute(FilterQuery query, CancellationToken cancellationToken = default)
        {
            if (!query.IsValid())
                throw new InvalidFilteredResultParams(query);

            var result = await repository.Filter(query.Page, query.Size);
            return result;
        }
    }
}
