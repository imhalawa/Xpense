using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Entities;

namespace Xpense.Persistence.Repositories
{
    public class MerchantRepository(XpenseDbContext context) : OptionRepository<Merchant>(context), IMerchantRepository
    {

    }
}
