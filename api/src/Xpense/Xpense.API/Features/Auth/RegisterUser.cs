using System.Data;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Auth;

public sealed class RegisterUser : IEndpoint
{
    private const int MaximumEncryptedValueLength = 4096;
    private const int ProtocolVersion = 1;

    /// <summary>
    /// Completes a passkey registration ceremony and provides the encrypted vault materials.
    /// </summary>
    public sealed record Request(
        Guid PendingRegistrationId,
        string? CredentialJson,
        string? EncryptionPublicKey,
        string? EncryptedPrivateKey,
        string? EncryptedPrivateKeyNonce,
        VaultWrapperRequest? VaultWrapper,
        int ProtocolVersion,
        string? InvitationToken);

    /// <summary>
    /// Provides the encrypted material required to unlock a vault with a passkey.
    /// </summary>
    public sealed record VaultWrapperRequest(string? Salt, string? Ciphertext, string? Nonce, string? Label);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.PendingRegistrationId)
                .NotEmpty().WithMessage("The pending registration is required.");
            RuleFor(request => request.CredentialJson)
                .NotEmpty().WithMessage("The passkey credential is required.");
            Base64(RuleFor(request => request.EncryptionPublicKey), "The encryption public key is required.", "The encryption public key must be valid and no larger than 4096 bytes.");
            Base64(RuleFor(request => request.EncryptedPrivateKey), "The encrypted private key is required.", "The encrypted private key must be valid and no larger than 4096 bytes.");
            Base64(RuleFor(request => request.EncryptedPrivateKeyNonce), "The encrypted private key nonce is required.", "The encrypted private key nonce must be valid and no larger than 4096 bytes.");
            RuleFor(request => request.VaultWrapper)
                .NotNull().WithMessage("The vault wrapper is required.");
            When(request => request.VaultWrapper is not null, () =>
            {
                Base64(RuleFor(request => request.VaultWrapper!.Salt), "The vault wrapper salt is required.", "The vault wrapper salt must be valid and no larger than 4096 bytes.");
                Base64(RuleFor(request => request.VaultWrapper!.Ciphertext), "The vault wrapper ciphertext is required.", "The vault wrapper ciphertext must be valid and no larger than 4096 bytes.");
                Base64(RuleFor(request => request.VaultWrapper!.Nonce), "The vault wrapper nonce is required.", "The vault wrapper nonce must be valid and no larger than 4096 bytes.");
                RuleFor(request => request.VaultWrapper!.Label)
                    .MaximumLength(100).WithMessage("The vault wrapper label must not exceed 100 characters.");
            });
            RuleFor(request => request.ProtocolVersion)
                .Equal(ProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/auth/register", Handle)
            .WithName(nameof(RegisterUser))
            .Validated()
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Created<CurrentIdentityResponse>> Handle(
        Request request,
        XpenseDbContext dbContext,
        UserManager<XpenseUser> userManager,
        SignInManager<XpenseUser> signInManager,
        IPasskeyHandler<XpenseUser> passkeyHandler,
        IOptions<XpenseAuthenticationOptions> authentication,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var pendingRegistration = await dbContext.PendingRegistrations.SingleOrDefaultAsync(
            registration => registration.Id == request.PendingRegistrationId,
            cancellationToken);

        if (pendingRegistration is null ||
            pendingRegistration.ConsumedAt is not null ||
            pendingRegistration.ExpiresAt <= DateTime.UtcNow)
            throw new RegistrationChallengeInvalidException();

        if (authentication.Value.Registration is RegistrationPolicy.Closed)
            throw new RegistrationNotAllowedException();

        if (authentication.Value.Registration is RegistrationPolicy.InviteOnly &&
            !await HasValidInvitation(request.InvitationToken, pendingRegistration.NormalizedEmail, dbContext, cancellationToken))
            throw new RegistrationNotAllowedException();

        var attestation = await passkeyHandler.PerformAttestationAsync(new PasskeyAttestationContext
        {
            HttpContext = httpContext,
            CredentialJson = request.CredentialJson!,
            AttestationState = pendingRegistration.AttestationState
        });

        if (!attestation.Succeeded ||
            attestation.UserEntity is null ||
            attestation.Passkey is null ||
            attestation.UserEntity.Name != pendingRegistration.NormalizedEmail ||
            !Guid.TryParse(attestation.UserEntity.Id, out var userId))
            throw new RegistrationChallengeInvalidException();

        var now = DateTime.UtcNow;
        var user = new XpenseUser
        {
            Id = userId,
            UserName = pendingRegistration.NormalizedEmail,
            NormalizedUserName = pendingRegistration.NormalizedEmail,
            Email = pendingRegistration.NormalizedEmail,
            NormalizedEmail = pendingRegistration.NormalizedEmail,
            State = AccountState.Active,
            CreatedAt = now
        };
        var created = await userManager.CreateAsync(user);

        if (!created.Succeeded)
            throw new RegistrationChallengeInvalidException();

        var vaultWrapper = request.VaultWrapper!;
        var encryptionIdentity = new UserEncryptionIdentity
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            PublicKey = Convert.FromBase64String(request.EncryptionPublicKey!),
            EncryptedPrivateKey = Convert.FromBase64String(request.EncryptedPrivateKey!),
            Nonce = Convert.FromBase64String(request.EncryptedPrivateKeyNonce!),
            ProtocolVersion = request.ProtocolVersion,
            CreatedAt = now
        };
        var wrapper = new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = VaultWrapperKind.Passkey,
            CredentialId = attestation.Passkey.CredentialId,
            Salt = Convert.FromBase64String(vaultWrapper.Salt!),
            Ciphertext = Convert.FromBase64String(vaultWrapper.Ciphertext!),
            Nonce = Convert.FromBase64String(vaultWrapper.Nonce!),
            ProtocolVersion = request.ProtocolVersion,
            Label = vaultWrapper.Label,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.UserEncryptionIdentities.Add(encryptionIdentity);
        dbContext.VaultWrappers.Add(wrapper);

        var passkeyCreated = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);

        if (!passkeyCreated.Succeeded)
            throw new RegistrationChallengeInvalidException();

        pendingRegistration.ConsumedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await signInManager.SignInAsync(user, false);

        return TypedResults.Created(
            httpContext.ResourceUri("/api/v1/auth/me"),
            CurrentIdentityResponse.Of(user, [wrapper]));
    }

    private static IRuleBuilderOptions<Request, string?> Base64(
        IRuleBuilderInitial<Request, string?> rule,
        string requiredMessage,
        string invalidMessage) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(requiredMessage)
            .Must(IsValidBase64).WithMessage(invalidMessage);

    private static bool IsValidBase64(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 5464)
            return false;

        Span<byte> decoded = stackalloc byte[MaximumEncryptedValueLength];
        return Convert.TryFromBase64String(value, decoded, out var bytesWritten) && bytesWritten > 0;
    }

    private static async Task<bool> HasValidInvitation(
        string? token,
        string normalizedEmail,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return await dbContext.GroupInvitations.AnyAsync(
            invitation => invitation.TokenHash.SequenceEqual(tokenHash) &&
                invitation.State == InvitationState.Pending &&
                invitation.ExpiresAt > DateTime.UtcNow &&
                (invitation.TargetNormalizedEmail == null || invitation.TargetNormalizedEmail == normalizedEmail) &&
                dbContext.Groups.Any(group => group.Id == invitation.GroupId && !group.IsDeleted),
            cancellationToken);
    }
}
