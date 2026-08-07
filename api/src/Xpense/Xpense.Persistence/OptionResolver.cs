using Microsoft.EntityFrameworkCore;
using Xpense.Domain.Entities;
using Xpense.Domain.Options;

namespace Xpense.Persistence;

public sealed class OptionResolver<TEntity>(XpenseDbContext dbContext)
    where TEntity : BaseEntity, IOptionEntity
{
    private DbSet<TEntity> Set => dbContext.Set<TEntity>();

    public async Task<TEntity?> Resolve<TModel>(TModel model, CancellationToken cancellationToken = default)
        where TModel : IOption<TEntity>
    {
        if (!model.Create && !model.Id.HasValue)
            return null;

        if (await FindLive(model, cancellationToken) is { } live)
            return live;

        if (await Restore(model, cancellationToken) is { } restored)
            return restored;

        return model.Create ? model.ToEntity() : null;
    }

    private async Task<TEntity?> FindLive<TModel>(TModel model, CancellationToken cancellationToken)
        where TModel : IOption<TEntity>
    {
        if (model.Id.HasValue &&
            await Set.FirstOrDefaultAsync(entity => entity.Id == model.Id.Value, cancellationToken) is { } byId)
            return byId;

        if (!string.IsNullOrWhiteSpace(model.Label) &&
            await Set.FirstOrDefaultAsync(entity => entity.Label == model.Label, cancellationToken) is { } byLabel)
            return byLabel;

        return null;
    }

    private async Task<TEntity?> Restore<TModel>(TModel model, CancellationToken cancellationToken)
        where TModel : IOption<TEntity>
    {
        var query = Set.IgnoreQueryFilters();

        var deleted = model.Id.HasValue
            ? await query.FirstOrDefaultAsync(entity => entity.Id == model.Id.Value, cancellationToken)
            : await query.FirstOrDefaultAsync(entity => entity.Label == model.Label, cancellationToken);

        if (deleted is not { IsDeleted: true })
            return null;

        deleted.IsDeleted = false;
        deleted.Touch();
        return deleted;
    }
}
