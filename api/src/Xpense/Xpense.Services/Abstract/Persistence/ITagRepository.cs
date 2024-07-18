using Xpense.Services.Entities;

namespace Xpense.Services.Abstract.Persistence;

public interface ITagRepository : IOptionRepository<Tag>
{
    int[] Exists(int[]? tagIds);
    Task<IEnumerable<Tag>> CreateRange(IEnumerable<Tag> tags);
}