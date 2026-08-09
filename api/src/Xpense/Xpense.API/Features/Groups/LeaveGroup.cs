using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xpense.API.Contracts;
using Xpense.API.Infrastructure;
using Xpense.API.Infrastructure.Authorization;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class LeaveGroup : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/groups/{groupId:guid}/leave", Handle)
            .WithName(nameof(LeaveGroup))
            .RequireAuthorization();

    private static async Task<Results<Ok<KeyRotationResponse>, NotFound>> Handle(
        Guid groupId,
        AccessRules accessRules,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var groupLock = await groupTransactionLock.Acquire(groupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (!await accessRules.IsActiveMember(groupId, cancellationToken))
            return TypedResults.NotFound();

        var userId = currentUser.Id;
        var membership = await dbContext.GroupMemberships.SingleOrDefaultAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId == userId &&
                membership.State == MembershipState.Active,
            cancellationToken);

        if (membership is null)
            return TypedResults.NotFound();

        if (membership.Role == MembershipRole.Owner)
            throw new GroupOwnerCannotLeaveException(groupId);

        var keyRotationRequired = membership.Revoke(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Ok(new KeyRotationResponse(keyRotationRequired));
    }
}
