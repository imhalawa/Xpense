using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Models;

namespace Xpense.Persistence.Repositories;

public class TransactionRepository(XpenseDbContext dbContext)
    : Repository<Transaction>(dbContext), ITransactionRepository
{
    public async Task<IEnumerable<Transaction>> GetAllTransactions(Account account)
    {
        var transactions = await GetBaseQuery().Where(t => t.Account == account).OrderByDescending(t => t.CreatedOn).ToListAsync();
        return transactions;
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactions()
    {
        var transactions = await GetBaseQuery().OrderByDescending(t => t.CreatedOn).ToListAsync();
        return transactions;
    }


    public async Task<PaginatedResult<Transaction>> Filter(int page, int pageSize)
    {
        if (page <= 0 || pageSize <= 0)
            return PaginatedResult<Transaction>.FromResult(0, 0, 0, Enumerable.Empty<Transaction>());

        var pages = DbSet.Count() / pageSize;
        var result = await GetBaseQuery()
            .Skip(pageSize * (page - 1))
            .Take(pageSize)
            .OrderByDescending(s => s.CreatedOn)
            .ToListAsync();

        return PaginatedResult<Transaction>.FromResult(page, pageSize, pages, result);
    }

    private IQueryable<Transaction> GetBaseQuery()
    {
        return DbSet
            .Include(t => t.Category)
            .ThenInclude(c => c.Priority)
            .Include(t => t.Merchant)
            .Include(t => t.Tags)
            .Include(t => t.Account);
    }
}