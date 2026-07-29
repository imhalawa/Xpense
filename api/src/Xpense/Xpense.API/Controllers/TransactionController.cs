using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Enums;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.UseCases;
using Xpense.Services.Models;

namespace Xpense.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/transactions")]
public class TransactionController(
    DepositTransactionUseCase depositTransactionUseCase,
    WithdrawTransactionUseCase withdrawTransactionUseCase,
    GetTransactionByIdUseCase getTransactionByIdUseCase,
    FilterTransactionsUseCase filterTransactionsUseCase)
    : XpenseController
{
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        if (!request.TryGetTransactionType(out var transactionType))
        {
            ModelState.AddModelError("type", "The type must be either 'income' or 'expense'.");
        }

        if (request.Amount is null)
        {
            ModelState.AddModelError("amount", "The amount is required.");
        }
        else
        {
            if (request.Amount.Cents <= 0)
                ModelState.AddModelError("amount.cents", "The amount in cents must be positive.");

            if (request.GetCurrency() is null)
                ModelState.AddModelError("amount.currency", "The currency must be a supported currency name.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = transactionType switch
        {
            TransactionType.Credit => await depositTransactionUseCase.Handle(request.ToDepositCommand()),
            TransactionType.Debit => await withdrawTransactionUseCase.Handle(request.ToWithdrawCommand()),
            _ => throw new UnsupportedTransactionTypeException(request.Type)
        };

        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, TransactionResponse.Of(transaction));
    }

    [HttpGet("{id:int}", Name = "Get Transaction By Id")]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await getTransactionByIdUseCase.Execute(id);
        return transaction is null
            ? new NotFoundResult()
            : new OkObjectResult(TransactionResponse.Of(transaction));
    }

    [HttpGet(Name = "Get Transactions")]
    public async Task<IActionResult> GetPage([FromQuery] int page, [FromQuery] int pageSize)
    {
        var result = await filterTransactionsUseCase.Execute(FilterQuery.Of(page, pageSize));
        return new OkObjectResult(TransactionPageResponse.Of(result));
    }

}
