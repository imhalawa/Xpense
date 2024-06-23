using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Xpense.Services.Models.Account;

namespace Xpense.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        [HttpGet("{id:int}", Name = "Get Account By (Id)")]
        public async Task<IActionResult> GetById(int id)
        {
            return Ok("Account");
        }

        [HttpGet("{accountNumber:minlength(10):maxlength(10)}", Name = "Get Account By (Account Number)")]
        public async Task<IActionResult> GetByAccountNumber(string accountNumber)
        {
            return Ok("Account");
        }

        [HttpPost("", Name = "Create Account", Order = 1)]
        public async Task<IActionResult> Create([FromBody] CreateAccount createAccount)
        {
            


            return Ok($"{createAccount.Name} Account Created");
        }

        [HttpDelete("{id:int}",Name ="Delete Account By Id")]
        public async Task<IActionResult> Delete(int id)
        {
            return Ok("");
        }

        [HttpDelete("{accountNumber:minlength(10):maxlength(10)}", Name = "Delete Account By Number")]
        public async Task<IActionResult> Delete(string number)
        {
            return Ok("");
        }
    }
}
