using FluentValidation;

namespace Xpense.API.Features.Tags;

internal static class TagColour
{
    /// <summary>Colours are accepted with or without a leading '#'; stored without.</summary>
    public static string Normalise(string hex) => hex?.TrimStart('#');
}

internal static class TagRules
{
    /// <summary>Shared by CreateTag and UpdateTag, which both validate the same two colours.</summary>
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
