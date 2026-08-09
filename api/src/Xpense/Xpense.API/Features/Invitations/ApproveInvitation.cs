using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class ApproveInvitation : IEndpoint
{
    private const int MaximumDecodedLength = 4096;
    private const int MaximumEncodedLength = 5464;
    private const int SupportedProtocolVersion = 1;

    /// <summary>
    /// Approves an awaiting membership with a client-produced encrypted group-key envelope.
    /// </summary>
    public sealed record Request(string? GroupKeyEnvelope, int ProtocolVersion);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.GroupKeyEnvelope)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("The group key envelope is required.")
                .Must(IsCanonicalBase64)
                .WithMessage("The group key envelope must be canonical Base64 containing 1 to 4096 bytes.");
            RuleFor(request => request.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The protocol version must be 1.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/invitations/{invitationId:guid}/approve", Handle)
            .WithName(nameof(ApproveInvitation))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid invitationId,
        Request request,
        AccessRules accessRules,
        GroupTransactionLock groupTransactionLock,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var groupId = await dbContext.GroupInvitations.AsNoTracking()
            .Where(invitation => invitation.Id == invitationId)
            .Select(invitation => (Guid?)invitation.GroupId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!groupId.HasValue)
            return TypedResults.NotFound();

        await using var groupLock = await groupTransactionLock.Acquire(groupId.Value, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await accessRules.IsGroupOwner(groupId.Value, cancellationToken))
            return TypedResults.NotFound();

        var invitation = await dbContext.GroupInvitations.SingleOrDefaultAsync(
            invitation =>
                invitation.Id == invitationId &&
                invitation.GroupId == groupId.Value &&
                invitation.State == InvitationState.AwaitingOwnerApproval &&
                invitation.AcceptedByUserId != null,
            cancellationToken);
        if (invitation is null)
            return TypedResults.NotFound();

        var membership = await dbContext.GroupMemberships.SingleOrDefaultAsync(
            membership =>
                membership.GroupId == groupId.Value &&
                membership.UserId == invitation.AcceptedByUserId &&
                membership.Role == MembershipRole.Member &&
                membership.State == MembershipState.AwaitingOwnerApproval &&
                membership.GroupKeyEnvelope == null,
            cancellationToken);
        if (membership is null)
            return TypedResults.NotFound();

        var now = DateTime.UtcNow;
        membership.Activate(
            Convert.FromBase64String(request.GroupKeyEnvelope!),
            request.ProtocolVersion,
            now);
        invitation.CompleteApproval(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static bool IsCanonicalBase64(string? envelope)
    {
        if (string.IsNullOrEmpty(envelope) || envelope.Length > MaximumEncodedLength)
            return false;

        Span<byte> decoded = stackalloc byte[MaximumDecodedLength];
        return Convert.TryFromBase64String(envelope, decoded, out var bytesWritten) &&
               bytesWritten > 0 &&
               Convert.ToBase64String(decoded[..bytesWritten]) == envelope;
    }
}
