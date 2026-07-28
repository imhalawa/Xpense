using System;
using Xpense.API.Contracts;
using Xpense.Domain.Entities;

namespace Xpense.API.Features.Accounts;

public sealed record AccountResponse(
    int Id,
    string AccountNumber,
    string Label,
    MoneyResponse Balance,
    bool IsDefault,
    long? CreatedOn,
    long? LastUpdated)
{
    public static AccountResponse Of(Account account) => new(
        account.Id,
        account.AccountNumber,
        account.Name,
        MoneyResponse.Of(account.Balance),
        account.IsDefaultAccount,
        new DateTimeOffset(account.CreatedOn).ToUnixTimeSeconds(),
        account.LastUpdated.HasValue
            ? new DateTimeOffset(account.LastUpdated.Value).ToUnixTimeSeconds()
            : null);
}
