using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public class TransactionRepository(XpenseDbContext dbContext)
    : Repository<Transaction>(dbContext), ITransactionRepository
{
    public async Task<IEnumerable<Transaction>> GetAllTransactions(Account account)
    {
        var baseQuery = DbSet
            .Include(t => t.Category)
            .ThenInclude(c => c.Priority)
            .Include(t => t.Merchant)
            .Include(t => t.Tags)
            .Include(t => t.Account);

        var transactions = await baseQuery.Where(t => t.Account == account).OrderByDescending(t => t.CreatedOn).ToListAsync();

        return transactions;
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactions()
    {
        var baseQuery = DbSet
            .Include(t => t.Category)
            .ThenInclude(c => c.Priority)
            .Include(t => t.Merchant)
            .Include(t => t.Tags)
            .Include(t => t.Account);

        var transactions = await baseQuery.OrderByDescending(t => t.CreatedOn).ToListAsync();

        return transactions;
    }
}