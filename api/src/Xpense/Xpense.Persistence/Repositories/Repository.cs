using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories
{
    public class Repository<T>(XpenseDbContext dbContext) : IRepository<T> where T : BaseEntity
    {
        protected readonly DbSet<T> DbSet = dbContext.Set<T>();

        public void Create(T entity)
        {
            DbSet.Add(entity);
        }

        public void Delete(T entity)
        {
            entity.MarkAsDeleted();
            entity.Touch();
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            return await DbSet.ToListAsync();
        }

        public async Task<T> GetById(int id)
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

        public async Task<IEnumerable<T>> Filter(int pageNumber, int pageSize)
        {
            return await DbSet.Skip(pageSize * (pageNumber - 1)).Take(pageSize).ToListAsync();
        }
    }
}