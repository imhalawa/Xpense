using System.Linq.Expressions;

namespace Xpense.Services.Abstract.Persistence
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAll<TK>(Expression<Func<T, TK>> includeExpr);
        Task<IEnumerable<T>> GetAll();
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