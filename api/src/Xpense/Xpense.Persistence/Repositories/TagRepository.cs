using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public class TagRepository(XpenseDbContext dbContext) : Repository<Tag>(dbContext), ITagRepository
{
    public int[] Exists(int[]? tagIds)
    {
        if (tagIds is null or { Length: 0 }) return [];
        return tagIds.Where(id => DbSet.Any(t => t.Id == id)).ToArray();
    }
}