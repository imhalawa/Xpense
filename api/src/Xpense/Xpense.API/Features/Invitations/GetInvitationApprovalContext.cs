using System;
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
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class GetInvitationApprovalContext : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/invitations/{invitationId:guid}/approval-context", Handle)
            .WithName(nameof(GetInvitationApprovalContext))
            .RequireAuthorization();

    private static async Task<Results<Ok<ApprovalContextResponse>, NotFound>> Handle(
        Guid invitationId,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
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
        var context = await (
                from invitation in dbContext.GroupInvitations.AsNoTracking()
                join encryptionIdentity in dbContext.UserEncryptionIdentities.AsNoTracking()
                    on invitation.AcceptedByUserId equals (Guid?)encryptionIdentity.UserId
                where invitation.Id == invitationId &&
                      invitation.GroupId == groupId.Value &&
                      invitation.State == InvitationState.AwaitingOwnerApproval &&
                      dbContext.Groups.Any(groupEntity =>
                          groupEntity.Id == invitation.GroupId &&
                          !groupEntity.IsDeleted &&
                          groupEntity.OwnerUserId == currentUser.Id) &&
                      dbContext.GroupMemberships.Any(membership =>
                          membership.GroupId == invitation.GroupId &&
                          membership.UserId == currentUser.Id &&
                          membership.Role == MembershipRole.Owner &&
                          membership.State == MembershipState.Active) &&
                      dbContext.GroupMemberships.Any(membership =>
                          membership.GroupId == invitation.GroupId &&
                          membership.UserId == encryptionIdentity.UserId &&
                          membership.Role == MembershipRole.Member &&
                          membership.State == MembershipState.AwaitingOwnerApproval &&
                          membership.GroupKeyEnvelope == null)
                select new
                {
                    invitation.Id,
                    encryptionIdentity.UserId,
                    encryptionIdentity.PublicKey,
                    encryptionIdentity.ProtocolVersion
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (context is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(new ApprovalContextResponse(
            context.Id,
            context.UserId,
            Convert.ToBase64String(context.PublicKey),
            context.ProtocolVersion));
    }
}
