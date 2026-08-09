using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xpense.API.Infrastructure;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Users;

public sealed class AddPasskey : IEndpoint
{
    private const int MaximumEncryptedValueLength = 4096;
    private const int ProtocolVersion = 1;

    /// <summary>
    /// Completes a passkey ceremony and stores its vault wrapper.
    /// </summary>
    public sealed record Request(
        Guid PendingRegistrationId,
        string? CredentialJson,
        VaultWrapperRequest? VaultWrapper,
        int ProtocolVersion);

    /// <summary>
    /// Provides the encrypted wrapper for the new passkey.
    /// </summary>
    public sealed record VaultWrapperRequest(
        string? Salt,
        string? Ciphertext,
        string? Nonce,
        string? Label);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.PendingRegistrationId)
                .NotEmpty().WithMessage("The pending passkey registration is required.");
            RuleFor(request => request.CredentialJson)
                .NotEmpty().WithMessage("The passkey credential is required.");
            RuleFor(request => request.VaultWrapper)
                .NotNull().WithMessage("The vault wrapper is required.");
            When(request => request.VaultWrapper is not null, () =>
            {
                Base64(
                    RuleFor(request => request.VaultWrapper!.Salt),
                    "The vault wrapper salt is required.",
                    "The vault wrapper salt must be valid and no larger than 4096 bytes.");
                Base64(
                    RuleFor(request => request.VaultWrapper!.Ciphertext),
                    "The vault wrapper ciphertext is required.",
                    "The vault wrapper ciphertext must be valid and no larger than 4096 bytes.");
                Base64(
                    RuleFor(request => request.VaultWrapper!.Nonce),
                    "The vault wrapper nonce is required.",
                    "The vault wrapper nonce must be valid and no larger than 4096 bytes.");
                RuleFor(request => request.VaultWrapper!.Label)
                    .MaximumLength(100).WithMessage("The passkey label must not exceed 100 characters.");
            });
            RuleFor(request => request.ProtocolVersion)
                .Equal(ProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/users/me/passkeys", Handle)
            .WithName(nameof(AddPasskey))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<Created<PasskeyResponse>, UnauthorizedHttpResult>> Handle(
        Request request,
        ClaimsPrincipal principal,
        UserManager<XpenseUser> userManager,
        IPasskeyHandler<XpenseUser> passkeyHandler,
        XpenseDbContext dbContext,
        IServiceScopeFactory serviceScopeFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
            return TypedResults.Unauthorized();

        var now = DateTime.UtcNow;
        var pendingRegistration = await dbContext.PendingRegistrations
            .AsNoTracking()
            .SingleOrDefaultAsync(registration =>
                registration.Id == request.PendingRegistrationId &&
                registration.NormalizedEmail == user.NormalizedEmail &&
                registration.ConsumedAt == null &&
                registration.ExpiresAt > now,
                cancellationToken);

        if (pendingRegistration is null)
            throw new PasskeyManagementInvalidException();

        var consumed = await dbContext.PendingRegistrations
            .Where(registration =>
                registration.Id == pendingRegistration.Id &&
                registration.ConsumedAt == null &&
                registration.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(registration => registration.ConsumedAt, now),
                cancellationToken);

        if (consumed != 1)
            throw new PasskeyManagementInvalidException();

        var attestation = await passkeyHandler.PerformAttestationAsync(new PasskeyAttestationContext
        {
            HttpContext = httpContext,
            CredentialJson = request.CredentialJson!,
            AttestationState = pendingRegistration.AttestationState
        });

        if (!attestation.Succeeded ||
            attestation.UserEntity is null ||
            attestation.Passkey is null ||
            attestation.UserEntity.Id != user.Id.ToString() ||
            attestation.UserEntity.Name != user.NormalizedEmail)
            throw new PasskeyManagementInvalidException();

        var wrapperRequest = request.VaultWrapper!;
        var wrapper = new VaultWrapper
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = VaultWrapperKind.Passkey,
            CredentialId = attestation.Passkey.CredentialId,
            Salt = Convert.FromBase64String(wrapperRequest.Salt!),
            Ciphertext = Convert.FromBase64String(wrapperRequest.Ciphertext!),
            Nonce = Convert.FromBase64String(wrapperRequest.Nonce!),
            ProtocolVersion = request.ProtocolVersion,
            Label = wrapperRequest.Label,
            CreatedAt = now,
            UpdatedAt = now
        };

        var credentialId = WebEncoders.Base64UrlEncode(attestation.Passkey.CredentialId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await using var credentialLockScope = serviceScopeFactory.CreateAsyncScope();
        var credentialLockDbContext = credentialLockScope.ServiceProvider.GetRequiredService<XpenseDbContext>();
        await using var credentialLockTransaction = await credentialLockDbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await credentialLockDbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({credentialId}, 0))",
            cancellationToken);

        if (await userManager.FindByPasskeyIdAsync(attestation.Passkey.CredentialId) is not null)
            throw new PasskeyManagementInvalidException();

        var credentialAdded = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);

        if (!credentialAdded.Succeeded)
            throw new PasskeyManagementInvalidException();

        dbContext.VaultWrappers.Add(wrapper);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await credentialLockTransaction.CommitAsync(cancellationToken);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/users/me/passkeys/{credentialId}"),
            new PasskeyResponse(credentialId, wrapper.Label, wrapper.LastUsedAt));
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
}
