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
    [Route("api/account")]
    public class AccountController(
        CreateAccountUseCase createAccount,
        GetAllAccountsUseCase getAllAccountsAccounts,
        DeleteAccountUseCase deleteAccountUseCase,
        GetAccountByNumberUseCase getAccountByNumberUseCase,
        UpdateAccountUseCase updateAccountUseCase,
        ILogger logger) : XpenseController
    {
        [HttpGet(
            "{accountNumber:minlength(10):maxlength(10)}",
            Name = "Get Account By (Account Number)"
        )]
        [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<IActionResult> GetByAccountNumber(string accountNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length != 10)
                    return ValidationProblem($"Please provide a valid account accountNumber");

                var account = await getAccountByNumberUseCase.Execute(accountNumber);
                return Ok(AccountResponse.Of(account));
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
            return Ok(accounts.Select(AccountResponse.Of));
        }

        [HttpPost("", Name = "Create Account", Order = 1)]
        public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
        {
            try
            {
                var createdAccount = await createAccount.Handle(request.ToCommand());
                return Ok(AccountResponse.Of(createdAccount));
            }
            catch (AccountCreationFailedException exception)
            {
                logger.Warning(exception.Message);
                return BadRequest(exception.Message);
            }
        }

        [HttpDelete(
            "{accountNumber:minlength(10):maxlength(10)}",
            Name = "Delete Account By Number"
        )]
        public async Task<IActionResult> Delete(string number)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(number) || number.Length != 10)
                    return ValidationProblem($"Please provide a valid account accountNumber");

                await deleteAccountUseCase.Handle(number);
                return Ok("Account Deleted Successfully");
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

        [HttpPut(Name = "Update account")]
        public async Task<IActionResult> Update([FromBody] UpdateAccountRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                    return ValidationProblem($"Invalid Patch Request: {ModelState}");
                var result = await updateAccountUseCase.Handle(request.ToCommand());
                return Ok(AccountResponse.Of(result));
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
    }
}