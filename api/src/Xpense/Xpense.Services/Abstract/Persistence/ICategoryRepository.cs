using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Persistence;

public interface ICategoryRepository: IRepository<Category>
{
    Task DeleteById(int id);
}