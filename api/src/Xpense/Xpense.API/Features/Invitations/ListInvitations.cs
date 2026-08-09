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

public sealed class ListInvitations : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/invitations", Handle)
            .WithName(nameof(ListInvitations))
            .RequireAuthorization();

    private static async Task<Results<Ok<InvitationResponse[]>, NotFound>> Handle(
        Guid groupId,
        AccessRules accessRules,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await accessRules.IsGroupOwner(groupId, cancellationToken))
            return TypedResults.NotFound();

        var currentUserId = currentUser.Id;
        var invitations = await (
            from invitation in dbContext.GroupInvitations.AsNoTracking()
            join delivery in dbContext.InvitationDeliveries.AsNoTracking()
                on invitation.Id equals delivery.InvitationId into deliveries
            from delivery in deliveries.DefaultIfEmpty()
            where invitation.GroupId == groupId &&
                  (invitation.State == InvitationState.Pending ||
                   invitation.State == InvitationState.AwaitingOwnerApproval) &&
                  dbContext.Groups.Any(groupEntity =>
                      groupEntity.Id == groupId &&
                      !groupEntity.IsDeleted &&
                      groupEntity.OwnerUserId == currentUserId) &&
                  dbContext.GroupMemberships.Any(membership =>
                      membership.GroupId == groupId &&
                      membership.UserId == currentUserId &&
                      membership.Role == MembershipRole.Owner &&
                      membership.State == MembershipState.Active)
            orderby invitation.CreatedAt descending, invitation.Id
            select new InvitationResponse(
                invitation.Id,
                invitation.GroupId,
                delivery == null ? null : delivery.EmailAddress,
                invitation.State,
                invitation.ExpiresAt,
                invitation.GroupKeyEnvelope != null,
                invitation.CreatedAt,
                invitation.UpdatedAt))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(invitations);
    }
}
