using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Accounts;

/// <summary>
/// The account number is the public identifier; the database key is deliberately not exposed, so
/// nothing can start depending on it. See docs/adr/0002-account-number-is-the-public-identifier.md.
/// </summary>
public sealed record AccountResponse(
    string AccountNumber,
    string Label,
    MoneyResponse Balance,
    bool IsDefault,
    string CreatedAt,
    string? UpdatedAt)
{
    public static AccountResponse Of(Account account) => new(
        account.AccountNumber,
        account.Label,
        MoneyResponse.Of(account.Balance),
        account.IsDefault,
        Timestamps.Iso(account.CreatedAt),
        Timestamps.Iso(account.UpdatedAt));
}
