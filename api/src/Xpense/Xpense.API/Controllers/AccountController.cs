using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Accounts.Requests;
using Xpense.Services.Features.Accounts.Responses;
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
        ILogger logger) : ControllerBase
    {
        [HttpGet(
            "{number:minlength(10):maxlength(10)}",
            Name = "Get Account By (Account Number)"
        )]
        [ProducesResponseType<GetAccountResponse>(StatusCodes.Status200OK, "application/json")]
        public async Task<IActionResult> GetByAccountNumber(string number)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(number) || number.Length != 10)
                    return ValidationProblem($"Please provide a valid account number");

                var result = await getAccountByNumberUseCase.Execute(number);
                return Ok(Models.Response.Ok(result));
            }
            catch (AccountNotFoundException ex)
            {
                return NotFound(Models.Response.NotFound(ex.Message));
            }
        }

        [HttpGet("", Name = "Get All Accounts")]
        public async Task<IActionResult> Get()
        {
            var accounts = await getAllAccountsAccounts.Execute();
            return Ok(Models.Response.Ok(accounts));
        }

        [HttpPost("", Name = "Create Account", Order = 1)]
        public async Task<IActionResult> Create([FromBody] CreateAccount request)
        {
            try
            {
                var result = await createAccount.Handle(request.ToCommand());
                logger.Information($"Attempt to create account: {request.Name}");
                return Ok(Models.Response.Ok(result));
            }
            catch (AccountCreationFailedException exception)
            {
                logger.Warning(exception.Message);
                return BadRequest(Models.Response.BadRequest(exception.Message));
            }
        }

        [HttpDelete(
            "{number:minlength(10):maxlength(10)}",
            Name = "Delete Account By Number"
        )]
        public async Task<IActionResult> Delete(string number)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(number) || number.Length != 10)
                    return ValidationProblem($"Please provide a valid account number");

                await deleteAccountUseCase.Handle(number);
                return Ok(Models.Response.Ok("Account Deleted Successfully"));
            }
            catch (AccountNotFoundException exception)
            {
                logger.Warning(exception.Message);
                return NotFound(Models.Response.NotFound(exception.Message));
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
            if (request == null || !ModelState.IsValid)
                return ValidationProblem($"Invalid Patch Request: {ModelState}");
            var result = await updateAccountUseCase.Handle(request.ToCommand());
            return Ok(Models.Response.Ok(result));
        }
    }
}