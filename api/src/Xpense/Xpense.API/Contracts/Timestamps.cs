using System;

namespace Xpense.API.Contracts;

/// <summary>
/// Every timestamp crosses the boundary as an ISO 8601 UTC string, per
/// docs/contract/api-v1-contract-design.md.
/// <para>
/// Shared because accounts, transactions, categories, priorities, tags and merchants all return
/// timestamps and they must look identical in each. Before this, accounts, categories, priorities,
/// tags and merchants returned unix seconds while transactions returned ISO 8601 -- one concept in
/// two formats, and the contract doc only described one of them.
/// </para>
/// </summary>
public static class Timestamps
{
    public static string Iso(DateTime value) =>
        new DateTimeOffset(value).ToUniversalTime().ToString("O");

    public static string? Iso(DateTime? value) =>
        value.HasValue ? Iso(value.Value) : null;
}
