using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xpense.Persistence;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Transactions;

internal static class TransactionQueries
{
    /// <summary>
    /// A transaction is only meaningful with its category, merchant, tags and account, and all
    /// three read slices need the same graph. Shared here rather than in a repository.
    /// </summary>
    public static IQueryable<Transaction> WithDetails(this XpenseDbContext db) =>
        db.Transactions
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Include(transaction => transaction.Merchant)
            .Include(transaction => transaction.Tags)
            .Include(transaction => transaction.Account);
}
