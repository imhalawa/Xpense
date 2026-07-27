using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Accounts.Usecases;

namespace Xpense.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/accounts")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public class AccountController(
        CreateAccountUseCase createAccount,
        GetAllAccountsUseCase getAllAccountsAccounts,
        GetAccountByIdUseCase getAccountByIdUseCase,
        DeleteAccountUseCase deleteAccountUseCase,
        UpdateAccountUseCase updateAccountUseCase) : ControllerBase
    {
        [HttpGet("{id:int}", Name = "Get Account By Id")]
        [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetById(int id)
        {
            var account = await getAccountByIdUseCase.Execute(id);
            return Ok(AccountResponse.Of(account));
        }

        [HttpGet("", Name = "Get All Accounts")]
        [ProducesResponseType<AccountResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var accounts = await getAllAccountsAccounts.Execute();
            return Ok(accounts.Select(AccountResponse.Of));
        }

        [HttpPost("", Name = "Create Account")]
        [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
        {
            var createdAccount = await createAccount.Handle(request.ToCommand());
            return CreatedAtAction(
                nameof(GetById),
                new { id = createdAccount.Id },
                AccountResponse.Of(createdAccount));
        }

        [HttpDelete("{id:int}", Name = "Delete Account By Id")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Delete(int id)
        {
            await deleteAccountUseCase.Handle(id);
            return NoContent();
        }

        [HttpPut("{id:int}", Name = "Update account")]
        [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountRequest request)
        {
            var result = await updateAccountUseCase.Handle(request.ToCommand(id));
            return Ok(AccountResponse.Of(result));
        }
    }
}
