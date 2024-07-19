using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Xpense.Services.Abstract.Entities;
using Xpense.Services.Abstract.Persistence;

namespace Xpense.Persistence.Repositories
{
    public class Repository<T>(XpenseDbContext dbContext) : IRepository<T> where T : BaseEntity
    {
        protected readonly DbSet<T> DbSet = dbContext.Set<T>();

        public async Task<T?> GetWithById<TK>(int id, Expression<Func<T, TK>> includeExpr)
        {
            return await DbSet.Include(includeExpr).FirstOrDefaultAsync(c => c.Id == id);
        }

        public void Create(T entity)
        {
            DbSet.Add(entity);
        }

        public void Delete(T entity)
        {
            entity.MarkAsDeleted();
            entity.Touch();
        }

        public async Task<IEnumerable<T>> GetAll<TK>(Expression<Func<T, TK>> includeExpr)
        {
            return await DbSet.Include(includeExpr).ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            return await DbSet.ToListAsync();
        }

        public async Task<T?> GetById(int id)
        {
            return await DbSet.FindAsync(id);
        }

        public void Update(T entity)
        {
            entity.Touch();
            DbSet.Update(entity);
        }

        public async Task<int> SaveChanges()
        {
            return await dbContext.SaveChangesAsync();
        }

        public async Task<bool> IsDeleted(int id)
        {
            return (await DbSet.IgnoreQueryFilters().FirstAsync(e => e.Id == id)).IsDeleted;
        }

        public async Task<bool> Restore(T? entity)
        {
            if (entity == null) return false;
            if (!entity.IsDeleted) return false;

            entity.IsDeleted = false;
            entity.Touch();
            return await SaveChanges() > 1;
        }

        public bool TryRestore(int id, out T? result)
        {
            var entity = DbSet.IgnoreQueryFilters().FirstOrDefault(e => e.Id == id);
            var isRestored = Restore(entity).Result;
            result = isRestored ? entity : null;
            return isRestored;
        }
    }
}