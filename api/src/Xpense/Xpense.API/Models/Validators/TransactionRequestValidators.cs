using FluentValidation;
using Xpense.API.Models.Requests;

namespace Xpense.API.Models.Validators;

public sealed class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(request => request.Type)
            .Must((request, value) => request.TryGetKind(out _))
            .WithMessage("The type must be either 'income' or 'expense'.");

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

        RuleFor(request => request.CategoryId)
            .GreaterThan(0).WithMessage("The categoryId must reference an existing category.");

        RuleFor(request => request.Merchant)
            .NotNull().WithMessage("The merchant is required.");
    }
}
