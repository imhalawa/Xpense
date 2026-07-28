using Xpense.Domain.ValueObjects;

namespace Xpense.API.Contracts;

/// <summary>
/// How money crosses the wire, per docs/contract/api-v1-contract-design.md.
/// <para>
/// Shared rather than duplicated because accounts, transactions, transfers and analytics all
/// return money and it must look identical in each. Four private copies of this record existed
/// before multi-currency landed.
/// </para>
/// </summary>
public sealed record MoneyResponse(long Cents, string Currency)
{
    public static MoneyResponse Of(Money money) => new(money.Cents, money.Currency.ToString());
}
