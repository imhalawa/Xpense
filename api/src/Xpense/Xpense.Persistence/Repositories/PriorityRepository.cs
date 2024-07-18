using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories
{
    public class PriorityRepository(XpenseDbContext dbContext) : Repository<Priority>(dbContext), IPriorityRepository
    {
    }
}
