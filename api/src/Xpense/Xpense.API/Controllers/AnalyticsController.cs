using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Analytics.UseCases;

namespace Xpense.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/analytics")]
    public class AnalyticsController(
        GetExpensesByCategoryUseCase getExpensesByCategoryUseCase
        ) : ControllerBase
    {
        [HttpGet("spending/by-category")]
        [ProducesResponseType<TodayExpensesByCategoryResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExpensesByCategory()
        {
            var result = await getExpensesByCategoryUseCase.Execute();
            return Ok(TodayExpensesByCategoryResponse.Of(result));
        }
    }
}
