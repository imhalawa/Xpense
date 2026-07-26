using System.Data;
using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public sealed class TransferRepository(XpenseDbContext dbContext) : ITransferRepository
{
    public async Task<Transfer> ExecuteAtomic(Func<Task<Transfer>> createTransfer)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var transfer = await createTransfer();
            dbContext.Transfers.Add(transfer);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return transfer;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
