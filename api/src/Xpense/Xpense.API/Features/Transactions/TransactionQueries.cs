using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xpense.Persistence;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Transactions;

internal static class TransactionQueries
{
    /// <summary>
    /// A transaction is only meaningful with its category, merchant, tags and both account sides,
    /// and every read slice needs the same graph. Shared here rather than in a repository.
    /// <para>
    /// Both accounts are loaded because the response reports them by account number, and either
    /// side may be absent -- a null side means the money crossed the system boundary.
    /// </para>
    /// </summary>
    public static IQueryable<Transaction> WithDetails(this XpenseDbContext db) =>
        db.Transactions
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Include(transaction => transaction.Merchant)
            .Include(transaction => transaction.Tags)
            .Include(transaction => transaction.SourceAccount)
            .Include(transaction => transaction.DestinationAccount);
}
