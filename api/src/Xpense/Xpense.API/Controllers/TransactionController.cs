using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Transactions.UseCases;
using Xpense.Services.Models;

namespace Xpense.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/transactions")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public class TransactionController(
    DepositTransactionUseCase depositTransactionUseCase,
    WithdrawTransactionUseCase withdrawTransactionUseCase,
    GetTransactionByIdUseCase getTransactionByIdUseCase,
    FilterTransactionsUseCase filterTransactionsUseCase)
    : ControllerBase
{
    /// <summary>Default paging, applied when the caller omits page/pageSize.</summary>
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;

    [HttpPost]
    [ProducesResponseType<V1TransactionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        // CreateTransactionRequestValidator has already rejected anything other than
        // income/expense, so the fall-through arm is unreachable.
        request.TryGetKind(out var kind);

        var transaction = kind switch
        {
            TransactionKind.Income => await depositTransactionUseCase.Handle(request.ToDepositCommand()),
            TransactionKind.Expense => await withdrawTransactionUseCase.Handle(request.ToWithdrawCommand()),
            _ => throw new InvalidOperationException("The transaction kind was validated before dispatch.")
        };

        return CreatedAtAction(nameof(GetV1ById), new { id = transaction.Id }, V1TransactionResponse.From(transaction));
    }

    [HttpGet("{id:int}", Name = "Get V1 Transaction By Id")]
    [ProducesResponseType<V1TransactionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetV1ById(int id)
    {
        var transaction = await getTransactionByIdUseCase.Execute(id);
        return Ok(V1TransactionResponse.From(transaction));
    }

    [HttpGet(Name = "Get V1 Transactions")]
    [ProducesResponseType<V1TransactionPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetV1Page([FromQuery] int page = DefaultPage, [FromQuery] int pageSize = DefaultPageSize)
    {
        var result = await filterTransactionsUseCase.Execute(FilterQuery.Of(page, pageSize));
        return Ok(V1TransactionPageResponse.From(result));
    }
}
