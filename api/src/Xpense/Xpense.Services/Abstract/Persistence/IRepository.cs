namespace Xpense.Services.Abstract.Persistence
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAll();
        Task<T> GetById(int id);
        void Add(T entity);
        void Delete(T entity);
        void Update(T entity);
        Task<IEnumerable<T>> Filter(int pageNumber, int pageSize);
        Task<int> SaveChanges();
    }
}