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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class RevokeInvitation : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/invitations/{invitationId:guid}", Handle)
            .WithName(nameof(RevokeInvitation))
            .RequireAuthorization();

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid invitationId,
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
                invitation.GroupId == groupId &&
                (invitation.State == InvitationState.Pending ||
                 invitation.State == InvitationState.AwaitingOwnerApproval),
            cancellationToken);
        if (invitation is null)
            return TypedResults.NotFound();

        GroupMembership? awaitingMembership = null;
        if (invitation.State == InvitationState.AwaitingOwnerApproval)
        {
            if (!invitation.AcceptedByUserId.HasValue)
                return TypedResults.NotFound();

            awaitingMembership = await dbContext.GroupMemberships.SingleOrDefaultAsync(
                membership =>
                    membership.GroupId == groupId.Value &&
                    membership.UserId == invitation.AcceptedByUserId.Value &&
                    membership.Role == MembershipRole.Member &&
                    membership.State == MembershipState.AwaitingOwnerApproval,
                cancellationToken);
            if (awaitingMembership is null)
                return TypedResults.NotFound();
        }

        var now = DateTime.UtcNow;
        awaitingMembership?.Revoke(now);
        invitation.Revoke(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
