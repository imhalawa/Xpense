using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Features.Transactions.UseCases;

namespace Xpense.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/transfers")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class TransferController(TransferTransactionUseCase transferTransactionUseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<V1TransferResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTransferRequest request)
    {
        // ponytail: 201 without a Location header -- there is no GET /api/v1/transfers/{id}
        // to point at yet. Switch to CreatedAtAction when that endpoint lands.
        var transfer = await transferTransactionUseCase.Handle(request.ToCommand());
        return StatusCode(StatusCodes.Status201Created, V1TransferResponse.From(transfer));
    }
}
