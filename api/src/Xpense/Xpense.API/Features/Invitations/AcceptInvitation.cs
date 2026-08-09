using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.API.Infrastructure.Invitations;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class AcceptInvitation : IEndpoint
{
    /// <summary>
    /// Accepts a canonical invitation bearer token for the authenticated account.
    /// </summary>
    public sealed record Request(string? Token);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/invitations/accept", Handle)
            .WithName(nameof(AcceptInvitation))
            .RequireAuthorization();

    private static async Task<Ok<AcceptInvitationResponse>> Handle(
        Request request,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!InvitationTokenCodec.TryHash(request.Token, out var tokenHash))
            throw new InvitationInvalidException();

        var groupId = await dbContext.GroupInvitations.AsNoTracking()
            .Where(invitation => invitation.TokenHash.SequenceEqual(tokenHash))
            .Select(invitation => (Guid?)invitation.GroupId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!groupId.HasValue)
            throw new InvitationInvalidException();

        await using var groupLock = await groupTransactionLock.Acquire(groupId.Value, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var invitation = await dbContext.GroupInvitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash.SequenceEqual(tokenHash) && invitation.GroupId == groupId.Value,
            cancellationToken);
        var group = await dbContext.Groups.SingleOrDefaultAsync(
            groupEntity => groupEntity.Id == groupId.Value && !groupEntity.IsDeleted,
            cancellationToken);
        if (invitation is null || group is null)
            throw new InvitationInvalidException();

        var userId = currentUser.Id;
        var user = await dbContext.Users.AsNoTracking().SingleAsync(user => user.Id == userId, cancellationToken);
        var membership = await dbContext.GroupMemberships.SingleOrDefaultAsync(
            membership => membership.GroupId == groupId.Value && membership.UserId == userId,
            cancellationToken);

        if (invitation.State != InvitationState.Pending)
        {
            if (invitation.AcceptedByUserId == userId &&
                invitation.State is InvitationState.Accepted or InvitationState.AwaitingOwnerApproval)
                throw new InvitationStateConflictException();

            throw new InvitationInvalidException();
        }

        var now = DateTime.UtcNow;
        if (invitation.ExpiresAt <= now ||
            invitation.TargetNormalizedEmail is not null &&
            invitation.TargetNormalizedEmail != user.NormalizedEmail)
            throw new InvitationInvalidException();

        if (group.OwnerUserId == userId ||
            membership?.Role == MembershipRole.Owner ||
            membership?.State is MembershipState.Active or MembershipState.AwaitingOwnerApproval)
            throw new InvitationStateConflictException();

        membership ??= new GroupMembership
        {
            Id = Guid.CreateVersion7(),
            GroupId = group.Id,
            UserId = userId,
            Role = MembershipRole.Member,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (dbContext.Entry(membership).State == EntityState.Detached)
            dbContext.GroupMemberships.Add(membership);

        var activatesImmediately = invitation.TargetNormalizedEmail is not null &&
            invitation.GroupKeyEnvelope is not null &&
            invitation.EnvelopeProtocolVersion.HasValue;
        if (invitation.GroupKeyEnvelope is not null && !invitation.EnvelopeProtocolVersion.HasValue)
            throw new InvitationInvalidException();

        if (activatesImmediately)
        {
            membership.Activate(
                invitation.GroupKeyEnvelope!,
                invitation.EnvelopeProtocolVersion!.Value,
                now);
            invitation.Accept(userId, now);
        }
        else
        {
            membership.AwaitOwnerApproval(now);
            invitation.AwaitOwnerApproval(userId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new AcceptInvitationResponse(
            group.Id,
            membership.State,
            !activatesImmediately,
            activatesImmediately ? Convert.ToBase64String(group.NameCiphertext) : null,
            activatesImmediately ? Convert.ToBase64String(group.NameNonce) : null,
            activatesImmediately ? group.ProtocolVersion : null,
            activatesImmediately ? Convert.ToBase64String(membership.GroupKeyEnvelope!) : null,
            activatesImmediately ? membership.EnvelopeProtocolVersion : null));
    }
}
