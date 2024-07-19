using Xpense.Services.Abstract.Persistence;
using Xpense.Services.Abstract.UseCases;
using Xpense.Services.Entities;

namespace Xpense.Services.Features.Merchants.UseCases
{
    public class GetAllMerchantsUseCase(IMerchantRepository repository) : IQueryHandler<IEnumerable<Merchant>>
    {
        public async Task<IEnumerable<Merchant>> Execute()
        {
            var result = await repository.GetAll();
            return result;
        }
    }
}
