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
using Xpense.API.Infrastructure.Authentication;
using Xpense.API.Infrastructure.Invitations;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Invitations;

public sealed class InspectInvitation : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/invitations/{token}", Handle)
            .WithName(nameof(InspectInvitation))
            .RequireRateLimiting(AuthenticationPolicyNames.RateLimit)
            .AllowAnonymous();

    private static async Task<Ok<InspectInvitationResponse>> Handle(
        string token,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!InvitationTokenCodec.TryHash(token, out var tokenHash))
            throw new InvitationInvalidException();

        var currentUserId = currentUser.IsAuthenticated ? currentUser.Id : Guid.Empty;
        var now = DateTime.UtcNow;
        var candidate = await (
            from invitation in dbContext.GroupInvitations.AsNoTracking()
            join groupEntity in dbContext.Groups.AsNoTracking() on invitation.GroupId equals groupEntity.Id
            join inviter in dbContext.Users.AsNoTracking() on invitation.InvitedByUserId equals inviter.Id
            where invitation.TokenHash.SequenceEqual(tokenHash) &&
                  invitation.State == InvitationState.Pending &&
                  invitation.ExpiresAt > now &&
                  !groupEntity.IsDeleted
            select new
            {
                invitation.GroupId,
                invitation.GroupKeyEnvelope,
                InviterEmail = inviter.Email!,
                groupEntity.NameCiphertext,
                groupEntity.NameNonce,
                groupEntity.ProtocolVersion,
                CanReadCiphertext = currentUser.IsAuthenticated &&
                    dbContext.GroupMemberships.Any(membership =>
                        membership.GroupId == invitation.GroupId &&
                        membership.UserId == currentUserId &&
                        membership.State == MembershipState.Active &&
                        membership.GroupKeyEnvelope != null)
            }).SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
            throw new InvitationInvalidException();

        return TypedResults.Ok(new InspectInvitationResponse(
            InvitationState.Pending,
            candidate.GroupKeyEnvelope is null,
            candidate.InviterEmail,
            candidate.CanReadCiphertext ? Convert.ToBase64String(candidate.NameCiphertext) : null,
            candidate.CanReadCiphertext ? Convert.ToBase64String(candidate.NameNonce) : null,
            candidate.CanReadCiphertext ? candidate.ProtocolVersion : null));
    }
}
