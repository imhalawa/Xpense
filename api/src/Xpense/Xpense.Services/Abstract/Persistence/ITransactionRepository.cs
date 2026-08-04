
using Xpense.Services.Entities;
using Xpense.Services.Models;

namespace Xpense.Services.Abstract.Persistence;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IEnumerable<Transaction>> GetAllTransactions(Account account);
    Task<IReadOnlyCollection<Transaction>> GetAllTransactions(long? date = null);
    Task<Transaction?> GetByIdWithDetails(int id);
    Task<PaginatedResult<Transaction>> Filter(int page, int pageSize, long? date = null);
}
