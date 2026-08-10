using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Xpense.API.Infrastructure.Invitations;

public static class InvitationTokenCodec
{
    private const int TokenLength = 32;

    public static GeneratedInvitationToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenLength);
        return new GeneratedInvitationToken(
            WebEncoders.Base64UrlEncode(bytes),
            SHA256.HashData(bytes));
    }

    public static bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(token);
            if (bytes.Length != TokenLength || WebEncoders.Base64UrlEncode(bytes) != token)
                return false;

            hash = SHA256.HashData(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record GeneratedInvitationToken(string Token, byte[] Hash);
