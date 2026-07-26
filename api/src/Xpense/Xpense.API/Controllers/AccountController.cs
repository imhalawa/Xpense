using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Linq;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Accounts.Usecases;


namespace Xpense.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/accounts")]
    public class AccountController(
        CreateAccountUseCase createAccount,
        GetAllAccountsUseCase getAllAccountsAccounts,
        DeleteAccountUseCase deleteAccountUseCase,
        UpdateAccountUseCase updateAccountUseCase,
        ILogger logger) : XpenseController
    {
        [HttpGet(
            "{id:int}",
            Name = "Get Account By Id"
        )]
        [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var account = await FindById(id);
                return new OkObjectResult(AccountResponse.Of(account));
            }
            catch (AccountNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("", Name = "Get All Accounts")]
        public async Task<IActionResult> Get()
        {
            var accounts = await getAllAccountsAccounts.Execute();
            return new OkObjectResult(accounts.Select(AccountResponse.Of));
        }

        [HttpPost("", Name = "Create Account", Order = 1)]
        public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
        {
            try
            {
                var createdAccount = await createAccount.Handle(request.ToCommand());
                return CreatedAtAction(nameof(GetById), new { id = createdAccount.Id }, AccountResponse.Of(createdAccount));
            }
            catch (AccountCreationFailedException exception)
            {
                logger.Warning(exception.Message);
                return BadRequest(exception.Message);
            }
        }

        [HttpDelete(
            "{id:int}",
            Name = "Delete Account By Id"
        )]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var account = await FindById(id);
                await deleteAccountUseCase.Handle(account.AccountNumber);
                return NoContent();
            }
            catch (AccountNotFoundException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
            catch (AccountUpdateFailedException exception)
            {
                logger.Warning(exception.Message);
                return Problem(exception.Message, statusCode: 500);
            }
        }

        [HttpPut("{id:int}", Name = "Update account")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                    return ValidationProblem($"Invalid Patch Request: {ModelState}");

                var account = await FindById(id);
                request.Number = account.AccountNumber;
                var result = await updateAccountUseCase.Handle(request.ToCommand());
                return new OkObjectResult(AccountResponse.Of(result));
            }
            catch (AccountNotFoundException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(exception.Message);
            }
            catch (AccountUpdateFailedException exception)
            {
                logger.Warning(exception.Message);
                return Problem(exception.Message);
            }
        }

        private async Task<Xpense.Services.Entities.Account> FindById(int id)
        {
            var account = (await getAllAccountsAccounts.Execute()).SingleOrDefault(account => account.Id == id);
            return account ?? throw new AccountNotFoundException(id.ToString());
        }
    }
}
