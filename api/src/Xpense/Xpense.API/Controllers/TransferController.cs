using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xpense.API.Models.Requests;
using Xpense.API.Models.Responses;
using Xpense.Services.Exceptions;
using Xpense.Services.Features.Transactions.UseCases;

namespace Xpense.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/transfers")]
public sealed class TransferController(TransferTransactionUseCase transferTransactionUseCase) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateTransferRequest request)
    {
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

        if (request.SourceAccountId == request.DestinationAccountId)
            ModelState.AddModelError("destinationAccountId", "Source and destination accounts must be different.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var transfer = await transferTransactionUseCase.Handle(request.ToCommand());
            return StatusCode(StatusCodes.Status201Created, TransferResponse.Of(transfer));
        }
        catch (InsufficientFundsForTransferException exception)
        {
            ModelState.AddModelError("amount.cents", exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidTransferException exception)
        {
            ModelState.AddModelError("transfer", exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (AccountNotFoundException exception)
        {
            return new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Account not found",
                Detail = exception.Message
            })
            {
                StatusCode = StatusCodes.Status404NotFound,
                ContentTypes = { "application/problem+json" }
            };
        }
    }
}
