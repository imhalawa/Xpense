using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Entities;
using Xpense.Services.Abstract.Models;
using Xpense.Services.Abstract.Persistence;

namespace Xpense.Persistence.Repositories
{
    public class OptionRepository<T>(XpenseDbContext context) : Repository<T>(context), IOptionRepository<T> where T : BaseEntity, IOptionEntity
    {
        public async Task<T?> GetByLabel(string label, bool ignoreFilters = false)
        {
            return !ignoreFilters
                ? await DbSet.FirstOrDefaultAsync(m => m.Label == label)
                : await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Label == label);
        }

        public bool TryRestore(string label, out T? result)
        {
            var entity = GetByLabel(label, true).Result;
            var isRestored = Restore(entity).Result;
            result = isRestored ? entity : null;
            return isRestored;
        }

        public async Task<T?> GetOrCreateIfMissing<K>(K model) where K : IOption<T>
        {
            // Potential Issues, in order to resolve an entity ~3 database calls are needed
            if (!model.Create && !model.Id.HasValue)
            {
                return null;
            }

            var result = model.ToEntity();

            switch (model.Create)
            {
                case true when !model.Id.HasValue && !string.IsNullOrWhiteSpace(model.Label):
                    {
                        var existingEntity = await GetByLabel(model.Label);
                        if (existingEntity != null)
                            result = existingEntity;

                        if (TryRestore(model.Label, out var restored))
                        {
                            result = restored;
                        }

                        break;
                    }
                case false when model.Id.HasValue:
                    {
                        var existingEntityById = await GetById(model.Id.Value);
                        if (existingEntityById != null)
                            result = existingEntityById;

                        // TODO: Should revisit this, basically it ignores the Id sent by client and attempt to retrieve any tag using the same name
                        var existingEntityByLabel = await GetByLabel(model.Label);
                        if (existingEntityByLabel != null)
                            result = existingEntityByLabel;

                        if (TryRestore(model.Id.Value, out var restored))
                        {
                            result = restored;
                        }

                        break;
                    }
            }

            return result;
        }
    }
}
