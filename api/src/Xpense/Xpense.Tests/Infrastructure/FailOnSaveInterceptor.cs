using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Xpense.Tests.Infrastructure;

public sealed class FailOnSaveInterceptor<TEntity> : SaveChangesInterceptor
    where TEntity : class
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Guard(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Guard(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Guard(DbContextEventData eventData)
    {
        var writing = eventData.Context?.ChangeTracker
            .Entries<TEntity>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified) ?? false;

        if (writing)
            throw new InvalidOperationException($"Simulated persistence failure writing {typeof(TEntity).Name}.");
    }
}
