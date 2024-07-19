using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
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
    GetAllTransactionsUseCase getAllTransactionsUseCase,
    GetAllTransactionsForAccountNumberUseCase getAllTransactionsForAccountNumberUseCase,
    ILogger logger)
    : XpenseController
{
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(TransactionResponse), 200)]
    public async Task<IActionResult> Income([FromBody] DepositTransactionRequest request)
    {
        try
        {
            var transaction = await depositTransactionUseCase.Handle(request.ToCommand());
            return Ok(TransactionResponse.Of(transaction));
        }
        catch (AccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (DefaultAccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (CategoryNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return BadRequest(exception.Message);
        }
        catch (MerchantNotFoundException exception)
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
    [ProducesResponseType(typeof(TransactionResponse), 200)]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawTransactionRequest request)
    {
        try
        {
            var transaction = await withdrawTransactionUseCase.Handle(request.ToCommand());
            return Ok(TransactionResponse.Of(transaction));
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

    [HttpGet("{accountNumber}", Name = "Get All Transactions for account")]
    public async Task<IActionResult> GetAll(string accountNumber)
    {
        try
        {
            var transactions = await getAllTransactionsForAccountNumberUseCase.Execute(accountNumber);
            return Ok(transactions.Select(TransactionResponse.Of));
        }
        catch (AccountNotFoundException exception)
        {
            logger.Warning(exception, exception.Message);
            return NotFound(exception.Message);
        }
    }

    [HttpGet("", Name = "Get All Transactions for user")]
    public async Task<IActionResult> GetAll()
    {

        var transactions = await getAllTransactionsUseCase.Execute();
        return Ok(transactions.Select(TransactionResponse.Of));
    }

    [HttpPost("transfer")]
    public Task<IActionResult> Transfer([FromBody] TransferTransactionRequest request)
    {
        throw new NotImplementedException();
    }
}