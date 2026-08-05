using Xpense.Domain.ValueObjects;

namespace Xpense.API.Contracts;

/// <summary>
/// How money crosses the wire, per docs/contract/api-v1-contract-design.md.
/// <para>
/// Shared rather than duplicated because accounts, transactions and analytics all return money and
/// it must look identical in each. Three private copies outlived the first attempt at this --
/// TransactionMoneyResponse, TransferMoneyResponse and a second MoneyResponse inside the analytics
/// slice -- and are now deleted.
/// </para>
/// <para>
/// The field is "minorUnits", not "cents": cents holds for EUR and USD and is wrong for the first
/// currency without them.
/// </para>
/// </summary>
public sealed record MoneyResponse(long MinorUnits, string Currency)
{
    public static MoneyResponse Of(Money money) => new(money.MinorUnits, money.Currency.ToString());
}
