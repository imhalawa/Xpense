using System.Linq.Expressions;
using Xpense.Services.Abstract.Entities;

namespace Xpense.Services.Abstract.Persistence
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<IEnumerable<T>> GetAll<TK>(Expression<Func<T, TK>> includeExpr);
        Task<IEnumerable<T>> GetAll();
        // TODO: Later extend to accept filters
        Task<T?> GetById(int id);
        Task<T?> GetWithById<TK>(int id, Expression<Func<T, TK>> includeExpr);
        void Create(T entity);
        void Delete(T entity);
        void Update(T entity);
        Task<int> SaveChanges();
        Task<bool> IsDeleted(int id);
        Task<bool> Restore(T? entity);
        bool TryRestore(int id, out T? result);
    }
}