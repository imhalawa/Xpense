using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;

namespace Xpense.API.Infrastructure.LegacyClaim;

public sealed class LegacyClaimTransactionLock(IServiceScopeFactory serviceScopeFactory)
{
    private const string LockKey = "legacy-claim-v1";

    public async Task<Lease> Acquire(CancellationToken cancellationToken)
    {
        return await Acquire(false, false, cancellationToken);
    }

    public async Task<Lease> AcquireSourceSnapshot(CancellationToken cancellationToken)
    {
        return await Acquire(true, false, cancellationToken);
    }

    public async Task<Lease> AcquireCompletionSnapshot(CancellationToken cancellationToken)
    {
        return await Acquire(true, true, cancellationToken);
    }

    private async Task<Lease> Acquire(
        bool lockSourceTables,
        bool lockTargetTables,
        CancellationToken cancellationToken)
    {
        var serviceScope = serviceScopeFactory.CreateAsyncScope();
        try
        {
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({LockKey}, 0))",
                cancellationToken);
            if (lockSourceTables)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "LOCK TABLE \"Xpense\".\"Accounts\", \"Xpense\".\"Priorities\", \"Xpense\".\"Categories\", \"Xpense\".\"Merchants\", \"Xpense\".\"Tags\", \"Xpense\".\"Transactions\", \"Xpense\".\"TransactionTags\", \"Xpense\".\"Budgets\", \"Xpense\".\"Notifications\" IN SHARE MODE",
                    cancellationToken);
            }
            if (lockTargetTables)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    "LOCK TABLE \"Xpense\".\"SharedResources\", \"Xpense\".\"EncryptedRecords\", \"Xpense\".\"RecordEnvelopes\" IN SHARE MODE",
                    cancellationToken);
            }
            return new Lease(serviceScope, transaction);
        }
        catch
        {
            await serviceScope.DisposeAsync();
            throw;
        }
    }

    public sealed class Lease(
        AsyncServiceScope serviceScope,
        IDbContextTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await serviceScope.DisposeAsync();
        }
    }
}
