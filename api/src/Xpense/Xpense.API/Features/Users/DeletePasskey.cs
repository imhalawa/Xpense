using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Users;

public sealed class DeletePasskey : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/users/me/passkeys/{credentialId}", Handle)
            .WithName(nameof(DeletePasskey))
            .RequireAuthorization();

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> Handle(
        string credentialId,
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        byte[] credentialBytes;

        try
        {
            credentialBytes = WebEncoders.Base64UrlDecode(credentialId);
        }
        catch (FormatException)
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({user.Id.ToString()}, 0))",
            cancellationToken);
        var passkey = await userManager.GetPasskeyAsync(user, credentialBytes);
        var passkeyWrappers = await dbContext.VaultWrappers
            .Where(wrapper => wrapper.UserId == user.Id && wrapper.Kind == VaultWrapperKind.Passkey)
            .ToListAsync(cancellationToken);
        var wrapper = passkeyWrappers.SingleOrDefault(item =>
            item.CredentialId is not null && item.CredentialId.SequenceEqual(credentialBytes));

        if (passkey is null || wrapper is null)
            return TypedResults.NotFound();

        var passkeys = await userManager.GetPasskeysAsync(user);
        var hasRemainingPasskeyWrapper = passkeys.Any(item =>
            !item.CredentialId.SequenceEqual(credentialBytes) &&
            passkeyWrappers.Any(passkeyWrapper =>
                passkeyWrapper.CredentialId is not null &&
                passkeyWrapper.CredentialId.SequenceEqual(item.CredentialId)));
        var hasRecoveryWrapper = await dbContext.VaultWrappers.AnyAsync(
            item => item.UserId == user.Id &&
                (item.Kind == VaultWrapperKind.RecoveryPassword ||
                    item.Kind == VaultWrapperKind.RecoveryFile && item.ConsumedAt == null),
            cancellationToken);

        if (!hasRemainingPasskeyWrapper && !hasRecoveryWrapper)
            throw new LastVaultWrapperException();

        var removed = await userManager.RemovePasskeyAsync(user, credentialBytes);

        if (!removed.Succeeded)
            throw new PasskeyManagementInvalidException();

        dbContext.VaultWrappers.Remove(wrapper);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
