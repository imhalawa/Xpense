using FluentValidation;
using Xpense.API.Models.Requests;

namespace Xpense.API.Models.Validators;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(request => request.Label)
            .NotEmpty().WithMessage("The label is required.")
            .MaximumLength(100);

        RuleFor(request => request.BgColorHex).HexColour("bgColorHex");
        RuleFor(request => request.FgColorHex).HexColour("fgColorHex");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(request => request.Label)
            .NotEmpty().WithMessage("The label is required.")
            .MaximumLength(100);

        RuleFor(request => request.BgColorHex).HexColour("bgColorHex");
        RuleFor(request => request.FgColorHex).HexColour("fgColorHex");
    }
}

internal static class TagValidationRules
{
    /// <summary>
    /// Colours are accepted with or without the leading '#'; the controller trims it before the
    /// value reaches the use case.
    /// </summary>
    public static IRuleBuilderOptions<T, string> HexColour<T>(this IRuleBuilder<T, string> rule, string fieldName)
    {
        return rule
            .NotEmpty().WithMessage($"The {fieldName} is required.")
            .Matches("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .WithMessage($"The {fieldName} must be a 3 or 6 digit hex colour, for example #1a2b3c.");
    }
}
