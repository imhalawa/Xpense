using Microsoft.AspNetCore.Mvc;

namespace Xpense.API.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        /// <summary>
        /// Gets an account by number
        /// </summary>
        /// <param name="number">The Account Number.</param>
        /// <returns></returns>
        [HttpGet("{number:length(10)}")]
        public ActionResult<string> Get(int number)
        {
            return Ok("Sample Account");
        }
    }
}
