using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Analytics.UseCases;

namespace Xpense.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/analytics")]
    public class AnalyticsController(
        GetExpensesByCategoryUseCase getExpensesByCategoryUseCase
        ) : XpenseController
    {
        [HttpGet("today/categories")]
        public async Task<IActionResult> GetExpensesByCategory()
        {
            var result = await getExpensesByCategoryUseCase.Execute();
            return Ok(TodayExpensesByCategoryResponse.Of(result));
        }
    }
}
