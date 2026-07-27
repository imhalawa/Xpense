using Microsoft.EntityFrameworkCore;
using Xpense.Domain.Abstract.Entities;
using Xpense.Domain.Abstract.Models;

namespace Xpense.Persistence;

/// <summary>
/// Resolves a client-supplied merchant or tag to a persisted entity: match by id, fall back to
/// label, undelete a soft-deleted row, or create when the caller asked for it.
/// <para>
/// This survived the move to vertical slices as a shared service rather than being inlined,
/// because the rules are subtle, two features depend on them, and getting them wrong silently
/// duplicates merchants. It used to be OptionRepository&lt;T&gt;.GetOrCreateIfMissing.
/// </para>
/// </summary>
public sealed class OptionResolver<TEntity>(XpenseDbContext db)
    where TEntity : BaseEntity, IOptionEntity
{
    private DbSet<TEntity> Set => db.Set<TEntity>();

    public async Task<TEntity?> Resolve<TModel>(TModel model, CancellationToken ct = default)
        where TModel : IOption<TEntity>
    {
        // Nothing to look up and no permission to create.
        if (!model.Create && !model.Id.HasValue)
            return null;

        if (await FindLive(model, ct) is { } live)
            return live;

        if (await Restore(model, ct) is { } restored)
            return restored;

        return model.Create ? model.ToEntity() : null;
    }

    private async Task<TEntity?> FindLive<TModel>(TModel model, CancellationToken ct)
        where TModel : IOption<TEntity>
    {
        if (model.Id.HasValue &&
            await Set.FirstOrDefaultAsync(entity => entity.Id == model.Id.Value, ct) is { } byId)
            return byId;

        if (!string.IsNullOrWhiteSpace(model.Label) &&
            await Set.FirstOrDefaultAsync(entity => entity.Label == model.Label, ct) is { } byLabel)
            return byLabel;

        return null;
    }

    /// <summary>Soft-deleted rows are hidden by the global filter, so look past it and undelete.</summary>
    private async Task<TEntity?> Restore<TModel>(TModel model, CancellationToken ct)
        where TModel : IOption<TEntity>
    {
        var query = Set.IgnoreQueryFilters();

        var deleted = model.Id.HasValue
            ? await query.FirstOrDefaultAsync(entity => entity.Id == model.Id.Value, ct)
            : await query.FirstOrDefaultAsync(entity => entity.Label == model.Label, ct);

        if (deleted is not { IsDeleted: true })
            return null;

        deleted.IsDeleted = false;
        deleted.Touch();
        return deleted;
    }
}
