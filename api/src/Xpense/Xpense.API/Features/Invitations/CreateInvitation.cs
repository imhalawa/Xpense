using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authentication;
using Xpense.API.Infrastructure.Authorization;
using Xpense.API.Infrastructure.Invitations;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Events;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class CreateInvitation : IEndpoint
{
    private const string DeliveryProtectorPurpose = "Xpense.InvitationDelivery";
    private const int MaximumDecodedLength = 4096;
    private const int MaximumEncodedLength = 5464;
    private const int SupportedProtocolVersion = 1;

    /// <summary>
    /// Creates a seven-day group invitation and optionally prepares encrypted delivery for one email address.
    /// </summary>
    public sealed record Request(
        Guid GroupId,
        string? TargetEmail,
        string? GroupKeyEnvelope,
        int ProtocolVersion);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.GroupId)
                .NotEmpty().WithMessage("The group is required.");
            RuleFor(request => request.TargetEmail)
                .Cascade(CascadeMode.Stop)
                .Must(email => email is null || !string.IsNullOrWhiteSpace(email))
                .WithMessage("The target email must not be empty.")
                .Must(email => email is null || email.Trim().Length <= 256)
                .WithMessage("The target email must not exceed 256 characters.")
                .Must(IsEmail)
                .WithMessage("The target email must be a valid email address.");
            RuleFor(request => request.GroupKeyEnvelope)
                .Cascade(CascadeMode.Stop)
                .Must((request, envelope) => envelope is null || !string.IsNullOrWhiteSpace(request.TargetEmail))
                .WithMessage("The group key envelope requires a target email.")
                .Must(envelope => envelope is null || IsCanonicalBase64(envelope))
                .WithMessage("The group key envelope must be canonical Base64 containing 1 to 4096 bytes.");
            RuleFor(request => request.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/invitations", Handle)
            .WithName(nameof(CreateInvitation))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<Created<CreatedInvitationResponse>, NotFound, ValidationProblem>> Handle(
        Request request,
        AccessRules accessRules,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        IEventBus eventBus,
        IDataProtectionProvider dataProtectionProvider,
        ILookupNormalizer lookupNormalizer,
        IOptions<XpenseAuthenticationOptions> authenticationOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await using var groupLock = await groupTransactionLock.Acquire(request.GroupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await accessRules.IsGroupOwner(request.GroupId, cancellationToken))
            return TypedResults.NotFound();

        var targetEmail = request.TargetEmail?.Trim();
        var normalizedEmail = targetEmail is null ? null : lookupNormalizer.NormalizeEmail(targetEmail);
        byte[]? envelope = null;
        if (request.GroupKeyEnvelope is not null)
        {
            var targetExists = await dbContext.Users.AsNoTracking().AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken);
            if (!targetExists)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["groupKeyEnvelope"] = ["The group key envelope requires a registered target email."]
                });
            }

            envelope = Convert.FromBase64String(request.GroupKeyEnvelope);
        }

        var now = DateTime.UtcNow;
        var generatedToken = InvitationTokenCodec.Generate();
        var invitationLink = $"{authenticationOptions.Value.PublicUrl.TrimEnd('/')}/invitations/{generatedToken.Token}";
        var invitation = new GroupInvitation
        {
            Id = Guid.CreateVersion7(),
            GroupId = request.GroupId,
            InvitedByUserId = currentUser.Id,
            TargetNormalizedEmail = normalizedEmail,
            TokenHash = generatedToken.Hash,
            State = InvitationState.Pending,
            ExpiresAt = now.AddDays(7),
            GroupKeyEnvelope = envelope,
            EnvelopeProtocolVersion = envelope is null ? null : SupportedProtocolVersion,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.GroupInvitations.Add(invitation);
        if (targetEmail is not null)
        {
            var protectedLink = dataProtectionProvider
                .CreateProtector(DeliveryProtectorPurpose)
                .Protect(invitationLink);
            dbContext.InvitationDeliveries.Add(new InvitationDelivery
            {
                Id = Guid.CreateVersion7(),
                InvitationId = invitation.Id,
                EmailAddress = targetEmail,
                ProtectedPayload = protectedLink,
                Status = DeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await eventBus.Emit(Event.Of(new GroupInvitationCreated(invitation.Id), now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/invitations/{invitation.Id}"),
            new CreatedInvitationResponse(
                invitation.Id,
                invitation.GroupId,
                targetEmail,
                invitation.State,
                invitation.ExpiresAt,
                envelope is not null,
                invitationLink));
    }

    private static bool IsEmail(string? email) =>
        email is null || MailAddress.TryCreate(email.Trim(), out var parsed) && parsed.Address == email.Trim();

    private static bool IsCanonicalBase64(string envelope)
    {
        if (envelope.Length is 0 or > MaximumEncodedLength)
            return false;

        Span<byte> decoded = stackalloc byte[MaximumDecodedLength];
        return Convert.TryFromBase64String(envelope, decoded, out var bytesWritten) &&
               bytesWritten > 0 &&
               Convert.ToBase64String(decoded[..bytesWritten]) == envelope;
    }
}
