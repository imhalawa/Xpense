using FluentValidation;
using Xpense.API.Models.Requests;

namespace Xpense.API.Models.Validators;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("The name is required.")
            .MaximumLength(200);

        RuleFor(request => request.PriorityId)
            .GreaterThan(0).WithMessage("The priorityId must reference an existing priority.");
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("The name is required.")
            .MaximumLength(200);

        RuleFor(request => request.PriorityId)
            .GreaterThan(0).WithMessage("The priorityId must reference an existing priority.");
    }
}
