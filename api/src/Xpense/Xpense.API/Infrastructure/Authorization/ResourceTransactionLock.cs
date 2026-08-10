using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xpense.Persistence;

namespace Xpense.API.Infrastructure.Authorization;

public sealed class ResourceTransactionLock(IServiceScopeFactory serviceScopeFactory)
{
    public async Task<Lease> Acquire(Guid resourceId, CancellationToken cancellationToken)
    {
        var serviceScope = serviceScopeFactory.CreateAsyncScope();
        try
        {
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
            var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            var lockKey = $"resource:{resourceId:N}";
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);
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
