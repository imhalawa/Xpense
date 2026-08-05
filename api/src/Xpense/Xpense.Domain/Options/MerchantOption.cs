using Xpense.Domain.Entities;

namespace Xpense.Domain.Options;

/// <summary>
/// A client's reference to a merchant: an id, a label, or a label plus permission to create it.
/// Named MerchantOption rather than Merchant so it stops colliding with the entity -- call
/// sites used to need a `using ServiceMerchant = ...` alias.
/// </summary>
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
