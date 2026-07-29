using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Merchants.UseCases;

namespace Xpense.API.Controllers
{
    [Route("api/v1/merchants")]
    [ApiController]
    public class MerchantController(
        GetAllMerchantsUseCase getAllMerchantsUse
    ) : XpenseController
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var merchants = await getAllMerchantsUse.Execute();
            return new OkObjectResult(merchants.Select(MerchantResponse.Of));
        }
    }
}
