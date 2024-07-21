using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Helpers;
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

    public async Task<PaginatedResult<Transaction>> Filter(int page, int pageSize, long? date = null)
    {
        if (page <= 0 || pageSize <= 0)
            return PaginatedResult<Transaction>.FromResult(0, 0, 0, Enumerable.Empty<Transaction>());

        var query = GetBaseQuery();
        if (date.HasValue)
        {
            var dateTime = date.ToDateTime();
            query = query.Where(t => t.CreatedOn.Date == dateTime!.Value.Date); // TODO: match the date only
        }
        var pages = query.Count() / pageSize + (query.Count() % pageSize > 0 ? 1 : 0);
        var result = await query.OrderByDescending(s => s.CreatedOn).Skip(pageSize * (page - 1))
            .Take(pageSize).ToListAsync();

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