using FluentValidation;
using Xpense.API.Models.Requests;

namespace Xpense.API.Models.Validators;

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator()
    {
        RuleFor(request => request.SourceAccountId)
            .GreaterThan(0).WithMessage("The sourceAccountId must reference an existing account.");

        RuleFor(request => request.DestinationAccountId)
            .GreaterThan(0).WithMessage("The destinationAccountId must reference an existing account.")
            .NotEqual(request => request.SourceAccountId)
            .WithMessage("Source and destination accounts must be different.");

        RuleFor(request => request.Amount)
            .NotNull().WithMessage("The amount is required.");

        When(request => request.Amount is not null, () =>
        {
            RuleFor(request => request.Amount.Cents)
                .GreaterThan(0).WithMessage("The amount in cents must be positive.");

            RuleFor(request => request.Amount.Currency)
                .Must((request, value) => request.TryGetCurrency(out _))
                .WithMessage("The currency must be a supported currency name.");
        });

        RuleFor(request => request.Reason)
            .MaximumLength(500);
    }
}
