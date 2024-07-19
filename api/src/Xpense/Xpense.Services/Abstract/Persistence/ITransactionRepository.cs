
using Xpense.Services.Entities;
using Xpense.Services.Models;

namespace Xpense.Services.Abstract.Persistence;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IEnumerable<Transaction>> GetAllTransactions(Account account);
    Task<IEnumerable<Transaction>> GetAllTransactions();
    Task<PaginatedResult<Transaction>> Filter(int page, int pageSize);
}