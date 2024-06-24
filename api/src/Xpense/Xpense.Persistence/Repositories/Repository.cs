using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        private readonly XpenseDbContext _dbContext;
        private readonly DbSet<T> _dbSet;

        public Repository(XpenseDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = _dbContext.Set<T>();
        }

        public void Add(T entity)
        {
            _dbSet.Add(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public async Task<IEnumerable<T>> GetAll()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T> GetById(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }
        public async Task<int> SaveChanges()
        {
            return await _dbContext.SaveChangesAsync();
        }

        public async Task<IEnumerable<T>> Filter(int pageNumber, int pageSize)
        {
            return await _dbSet.Skip(pageSize * (pageNumber - 1)).Take(pageSize).ToListAsync();
        }
    }
}
