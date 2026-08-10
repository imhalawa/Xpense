using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Xpense.API.Infrastructure.LegacyClaim;

public static class LegacyClaimTokenCodec
{
    public static GeneratedClaimToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return new GeneratedClaimToken(
            WebEncoders.Base64UrlEncode(bytes),
            SHA256.HashData(bytes));
    }

    public static bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrEmpty(token))
            return false;

        byte[] bytes;
        try
        {
            bytes = WebEncoders.Base64UrlDecode(token);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length != 32 || WebEncoders.Base64UrlEncode(bytes) != token)
            return false;

        hash = SHA256.HashData(bytes);
        return true;
    }

    public sealed record GeneratedClaimToken(string Token, byte[] Hash);
}
