using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.UseCases;

namespace Xpense.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/transaction")]
public class TransactionController(
    DepositTransactionUseCase depositTransactionUseCase,
    WithdrawTransactionUseCase withdrawTransactionUseCase,
    ILogger logger)
    : XpenseController
{
    [HttpPost("deposit")]
    public async Task<IActionResult> Income([FromBody] DepositTransactionRequest request)
    {
        try
        {
            var transaction = await depositTransactionUseCase.Handle(request.ToCommand());
            return Ok(DepositTransactionResponse.Of(transaction).ToString());
        }
        catch (AccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (CategoryNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (DefaultAccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (DepositCreationFailedException exception)
        {
            logger.Error(exception, exception.Message);
            return Problem(exception.Message);
        }
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawTransactionRequest request)
    {
        try
        {
            await withdrawTransactionUseCase.Handle(request.ToCommand());
            return Ok($"The account with number {request.FromAccount} has been debited with {request.Amount}");
        }
        catch (AccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (CategoryNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
        catch (DefaultAccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (WithdrawCreationFailedException exception)
        {
            logger.Error(exception, exception.Message);
            return Problem(exception.Message);
        }
    }

    [HttpPost("transfer")]
    public Task<IActionResult> Transfer([FromBody] TransferTransactionRequest request)
    {
        throw new NotImplementedException();
    }
}