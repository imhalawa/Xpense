using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public class TagRepository(XpenseDbContext dbContext) : OptionRepository<Tag>(dbContext), ITagRepository
{
    public async Task<IEnumerable<Tag>> CreateRange(IEnumerable<Tag> tags)
    {
        await DbSet.AddRangeAsync(tags);
        return tags;
    }

    public int[] Exists(int[]? tagIds)
    {
        if (tagIds is null or { Length: 0 }) return Array.Empty<int>();
        return tagIds.Where(id => DbSet.Any(t => t.Id == id)).ToArray();
    }
}