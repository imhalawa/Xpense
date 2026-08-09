using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Xpense.Domain.Entities;

namespace Xpense.API.Infrastructure.Authentication;

public interface IRecoveryPasswordVerifier
{
    Task<bool> VerifyAsync(XpenseUser? user, string password);
}

public sealed class RecoveryPasswordVerifier(
    SignInManager<XpenseUser> signInManager,
    IPasswordHasher<XpenseUser> passwordHasher,
    RecoveryPasswordTimingHash timingHash) : IRecoveryPasswordVerifier
{
    private const string SyntheticUserName = "recovery-password-timing";

    public async Task<bool> VerifyAsync(XpenseUser? user, string password)
    {
        if (user is not null)
            return (await signInManager.CheckPasswordSignInAsync(user, password, true)).Succeeded;

        var syntheticUser = new XpenseUser
        {
            Id = Guid.Empty,
            UserName = SyntheticUserName
        };
        passwordHasher.VerifyHashedPassword(syntheticUser, timingHash.Value, password);
        return false;
    }
}

public sealed class RecoveryPasswordTimingHash
{
    private const string SyntheticPassword = "Synthetic recovery timing password 1!";
    private static readonly PasswordHasher<XpenseUser> PasswordHasher = new();

    private RecoveryPasswordTimingHash(string value) => Value = value;

    public string Value { get; }

    public static RecoveryPasswordTimingHash Create()
    {
        var user = new XpenseUser
        {
            Id = Guid.Empty,
            UserName = "recovery-password-timing"
        };
        return new RecoveryPasswordTimingHash(PasswordHasher.HashPassword(user, SyntheticPassword));
    }
}
