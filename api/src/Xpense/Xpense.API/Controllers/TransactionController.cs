using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
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
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request)
    {
        // The validator has already rejected anything other than income/expense; requiring it
        // here means a caller that bypassed validation still fails loudly.
        var transactionType = request.RequireTransactionType();

        var transaction = transactionType switch
        {
            TransactionType.Credit => await depositTransactionUseCase.Handle(request.ToDepositCommand()),
            TransactionType.Debit => await withdrawTransactionUseCase.Handle(request.ToWithdrawCommand()),
            _ => throw new UnsupportedTransactionTypeException(request.Type)
        };

        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, TransactionResponse.Of(transaction));
    }

    [HttpGet("{id:int}", Name = "Get Transaction By Id")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await getTransactionByIdUseCase.Execute(id);
        return Ok(TransactionResponse.Of(transaction));
    }

    [HttpGet(Name = "Get Transactions")]
    [ProducesResponseType<TransactionPageResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage([FromQuery] int page = DefaultPage, [FromQuery] int pageSize = DefaultPageSize)
    {
        var result = await filterTransactionsUseCase.Execute(FilterQuery.Of(page, pageSize));
        return Ok(TransactionPageResponse.Of(result));
    }
}
