using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Merchants;

public sealed record MerchantResponse(
    int Id,
    string Label,
    string CreatedAt,
    string? UpdatedAt)
{
    public static MerchantResponse Of(Merchant merchant) => new(
        merchant.Id,
        merchant.Label,
        Timestamps.Iso(merchant.CreatedAt),
        Timestamps.Iso(merchant.UpdatedAt));
}
