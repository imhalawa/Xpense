using FluentValidation;

namespace Xpense.API.Features.Tags;

internal static class TagColour
{
    public static string Normalise(string hex) => hex?.TrimStart('#');
}

internal static class TagRules
{
    public static IRuleBuilderOptions<T, string> HexColour<T>(
        this IRuleBuilder<T, string> rule,
        string fieldName)
    {
        return rule
            .NotEmpty().WithMessage($"The {fieldName} is required.")
            .Matches("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .WithMessage($"The {fieldName} must be a 3 or 6 digit hex colour, for example #1a2b3c.");
    }
}
