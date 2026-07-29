using FluentValidation;
using Xpense.API.Models.Requests;

namespace Xpense.API.Models.Validators;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("The name is required.")
            .MaximumLength(200);

        RuleFor(request => request.Balance)
            .GreaterThanOrEqualTo(0).WithMessage("The opening balance cannot be negative.");
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("The name is required.")
            .MaximumLength(200);
    }
}
