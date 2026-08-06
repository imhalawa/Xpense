using Xpense.Domain.Entities;

namespace Xpense.Domain.Options;

public class MerchantOption : IOption<Merchant>
{
    public int? Id { get; set; }
    public required string Label { get; set; }
    public bool Create { get; set; }

    public Merchant ToEntity() => new()
    {
        Label = Label,
        CreatedAt = DateTime.UtcNow
    };
}
