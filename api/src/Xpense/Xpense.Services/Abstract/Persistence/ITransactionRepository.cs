
using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Persistence;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<IEnumerable<Transaction>> GetAllTransactions(string accountNumber);
}