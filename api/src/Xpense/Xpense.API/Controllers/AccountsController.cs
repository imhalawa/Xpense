using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Xpense.API.Controllers
{
    [Route("api/accounts")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        [HttpGet("{accoundId:int}")]
        public async Task<ActionResult<string>> GetAccount(int accountId)
        {
            return Ok(Task.FromResult("Sample Account"));
        }
    }
}
