using System;

namespace Xpense.API.Contracts;

public static class Timestamps
{
    public static string Iso(DateTime value) =>
        new DateTimeOffset(value).ToUniversalTime().ToString("O");

    public static string? Iso(DateTime? value) =>
        value.HasValue ? Iso(value.Value) : null;
}
