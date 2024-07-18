using Xpense.Services.Abstract.Entities;
using Xpense.Services.Abstract.Models;

namespace Xpense.Services.Abstract.Persistence
{
    public interface IOptionRepository<T> : IRepository<T> where T : BaseEntity, IOptionEntity
    {
        Task<T?> GetByLabel(string Label, bool ignoreFilters = false);
        bool TryRestore(string Label, out T? result);
        Task<T?> GetOrCreateIfMissing<K>(K model) where K : IOption<T>;
    }
}
