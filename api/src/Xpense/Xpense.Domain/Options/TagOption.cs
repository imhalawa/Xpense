using Xpense.Domain.Entities;

namespace Xpense.Domain.Options;

public class TagOption : IOption<Tag>
{
    public int? Id { get; set; }
    public required string Label { get; set; }
    public bool Create { get; set; }

    public Tag ToEntity() => new()
    {
        Label = Label,
        CreatedAt = DateTime.UtcNow
    };
}
