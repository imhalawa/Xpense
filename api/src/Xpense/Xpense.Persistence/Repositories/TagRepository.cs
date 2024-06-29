using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories;

public class TagRepository(XpenseDbContext dbContext) : Repository<Tag>(dbContext), ITagRepository
{
    
}