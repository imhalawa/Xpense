using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xpense.Persistence;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Transactions;

internal static class TransactionQueries
{
    public static IQueryable<Transaction> WithDetails(this XpenseDbContext dbContext) =>
        dbContext.Transactions
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.Priority)
            .Include(transaction => transaction.Merchant)
            .Include(transaction => transaction.Tags)
            .Include(transaction => transaction.SourceAccount)
            .Include(transaction => transaction.DestinationAccount);
}
