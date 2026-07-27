using Xpense.Domain.Entities;

namespace Xpense.Domain.Options;

/// <summary>
/// A client's reference to a tag. See <see cref="MerchantOption"/> for the naming.
/// </summary>
public class TagOption : IOption<Tag>
{
    public int? Id { get; set; }
    public required string Label { get; set; }
    public bool Create { get; set; }

    public Tag ToEntity() => new()
    {
        Label = Label,
        CreatedOn = DateTime.UtcNow
    };
}
