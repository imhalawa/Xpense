using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Xpense.API.Helpers;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
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
    [ProducesResponseType(typeof(V1TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        if (!request.TryGetKind(out var kind))
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

            if (!request.TryGetCurrency(out _))
                ModelState.AddModelError("amount.currency", "The currency must be a supported currency name.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var transaction = kind switch
        {
            TransactionKind.Income => await depositTransactionUseCase.Handle(request.ToDepositCommand()),
            TransactionKind.Expense => await withdrawTransactionUseCase.Handle(request.ToWithdrawCommand()),
            _ => throw new InvalidOperationException("The transaction kind was validated before dispatch.")
        };

        return CreatedAtAction(nameof(GetV1ById), new { id = transaction.Id }, V1TransactionResponse.From(transaction));
    }

    [HttpGet("{id:int}", Name = "Get V1 Transaction By Id")]
    public async Task<IActionResult> GetV1ById(int id)
    {
        var transaction = await getTransactionByIdUseCase.Execute(id);
        return transaction is null
            ? new NotFoundResult()
            : new OkObjectResult(V1TransactionResponse.From(transaction));
    }

    [HttpGet(Name = "Get V1 Transactions")]
    public async Task<IActionResult> GetV1Page([FromQuery] int page, [FromQuery] int pageSize)
    {
        var result = await filterTransactionsUseCase.Execute(FilterQuery.Of(page, pageSize));
        return new OkObjectResult(V1TransactionPageResponse.From(result));
    }

}
