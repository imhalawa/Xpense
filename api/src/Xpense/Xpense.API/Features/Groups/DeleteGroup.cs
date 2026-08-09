using System;
using System.Data;
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
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class DeleteGroup : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/v1/groups/{groupId:guid}", Handle)
            .WithName(nameof(DeleteGroup))
            .RequireAuthorization();

    private static async Task<Results<NoContent, NotFound>> Handle(
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

        if (!await accessRules.IsGroupOwner(groupId, cancellationToken))
            return TypedResults.NotFound();

        var userId = currentUser.Id;
        var group = await dbContext.Groups.SingleOrDefaultAsync(
            group => group.Id == groupId && !group.IsDeleted,
            cancellationToken);

        if (group is null)
            return TypedResults.NotFound();

        var hasOtherActiveMember = await dbContext.GroupMemberships.AsNoTracking().AnyAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId != userId &&
                membership.State == MembershipState.Active,
            cancellationToken);

        if (hasOtherActiveMember)
            throw new GroupNotEmptyException(groupId);

        group.MarkAsDeleted();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
