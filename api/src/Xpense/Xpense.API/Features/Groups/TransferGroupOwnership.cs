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

namespace Xpense.API.Features.Groups;

public sealed class TransferGroupOwnership : IEndpoint
{
    /// <summary>
    /// Transfers group ownership to a different active member with a key envelope.
    /// </summary>
    public sealed record Request(Guid NewOwnerUserId);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.NewOwnerUserId)
                .NotEmpty().WithMessage("The new owner is required.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/groups/{groupId:guid}/ownership", Handle)
            .WithName(nameof(TransferGroupOwnership))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<NoContent, NotFound>> Handle(
        Guid groupId,
        Request request,
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

        var currentUserId = currentUser.Id;
        if (request.NewOwnerUserId == currentUserId ||
            !await accessRules.IsGroupOwner(groupId, cancellationToken))
            return TypedResults.NotFound();

        var group = await dbContext.Groups.SingleOrDefaultAsync(
            group => group.Id == groupId && !group.IsDeleted,
            cancellationToken);
        var memberships = await dbContext.GroupMemberships
            .Where(membership =>
                membership.GroupId == groupId &&
                (membership.UserId == currentUserId ||
                 membership.UserId == request.NewOwnerUserId ||
                 (membership.Role == MembershipRole.Owner && membership.State == MembershipState.Active)))
            .ToArrayAsync(cancellationToken);
        var activeOwners = memberships.Where(membership =>
            membership.Role == MembershipRole.Owner &&
            membership.State == MembershipState.Active).ToArray();
        var currentOwner = activeOwners.Length == 1 && activeOwners[0].UserId == currentUserId
            ? activeOwners[0]
            : null;
        var newOwner = memberships.SingleOrDefault(membership =>
            membership.UserId == request.NewOwnerUserId &&
            membership.Role == MembershipRole.Member &&
            membership.State == MembershipState.Active &&
            membership.GroupKeyEnvelope != null);

        if (group is null ||
            group.OwnerUserId != currentUserId ||
            currentOwner is null ||
            newOwner is null)
            return TypedResults.NotFound();

        var now = DateTime.UtcNow;
        currentOwner.ChangeRole(MembershipRole.Member, now);
        newOwner.ChangeRole(MembershipRole.Owner, now);
        group.TransferOwnership(newOwner.UserId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
