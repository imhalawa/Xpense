using Microsoft.EntityFrameworkCore;
using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;
using Xpense.Services.Exceptions;

namespace Xpense.Persistence.Repositories;

public class CategoryRepository(XpenseDbContext dbContext) : Repository<Category>(dbContext), ICategoryRepository
{
    public async Task DeleteById(int id)
    {
        try
        {
            var category = await DbSet.FirstAsync(e => e.Id == id);
            category.MarkAsDeleted();
            DbSet.Update(category);
        }
        catch (Exception ex)
        {
            throw new CategoryNotFoundException(id, ex);
        }
    }
}