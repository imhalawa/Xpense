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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Groups;

public sealed class CreateResourceGrant : IEndpoint
{
    /// <summary>
    /// Grants one group Viewer or Editor access to a caller-owned account or budget.
    /// </summary>
    public sealed record Request(
        SharedResourceType ResourceType,
        Guid ResourceId,
        GrantPermission Permission,
        Guid? KeyEnvelopeReference);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.ResourceType)
                .IsInEnum().WithMessage("The resource type must be a valid selection.");
            RuleFor(request => request.ResourceId)
                .NotEmpty().WithMessage("The resource is required.");
            RuleFor(request => request.Permission)
                .IsInEnum().WithMessage("The grant permission must be a valid selection.");
            RuleFor(request => request.KeyEnvelopeReference)
                .Must(reference => !reference.HasValue || reference.Value != Guid.Empty)
                .WithMessage("The key envelope reference must be a valid selection.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/groups/{groupId:guid}/grants", Handle)
            .WithName(nameof(CreateResourceGrant))
            .Validated()
            .RequireAuthorization();

    private static async Task<Results<Created<ResourceGrantResponse>, NotFound>> Handle(
        Guid groupId,
        Request request,
        ResourceTransactionLock resourceTransactionLock,
        GroupTransactionLock groupTransactionLock,
        ICurrentUser currentUser,
        XpenseDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await using var resourceLock = await resourceTransactionLock.Acquire(
            request.ResourceId,
            cancellationToken);
        await using var groupLock = await groupTransactionLock.Acquire(groupId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var userId = currentUser.Id;
        var canGrant = await dbContext.GroupMemberships.AsNoTracking().AnyAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId == userId &&
                membership.State == MembershipState.Active &&
                dbContext.Groups.Any(groupEntity =>
                    groupEntity.Id == membership.GroupId &&
                    !groupEntity.IsDeleted),
            cancellationToken);
        if (!canGrant)
            return TypedResults.NotFound();

        var resource = await dbContext.SharedResources.SingleOrDefaultAsync(
            candidate => candidate.Id == request.ResourceId,
            cancellationToken);
        if (resource is not null &&
            (resource.OwnerUserId != userId || resource.Type != request.ResourceType))
            return TypedResults.NotFound();

        if (await dbContext.ResourceGrants.AnyAsync(
                grant =>
                    grant.GroupId == groupId &&
                    grant.ResourceType == request.ResourceType &&
                    grant.ResourceId == request.ResourceId &&
                    grant.State == GrantState.Active,
                cancellationToken))
            throw new ResourceGrantAlreadyActiveException();

        var now = DateTime.UtcNow;
        if (resource is null)
        {
            resource = new SharedResource
            {
                Id = request.ResourceId,
                Type = request.ResourceType,
                OwnerUserId = userId,
                CreatedAt = now
            };
            dbContext.SharedResources.Add(resource);
        }

        var grant = await dbContext.ResourceGrants
            .Where(candidate =>
                candidate.GroupId == groupId &&
                candidate.ResourceType == request.ResourceType &&
                candidate.ResourceId == request.ResourceId &&
                candidate.State == GrantState.Revoked)
            .OrderByDescending(candidate => candidate.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (grant is null)
        {
            grant = new ResourceGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = groupId,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                CreatedAt = now
            };
            dbContext.ResourceGrants.Add(grant);
        }

        grant.Activate(request.Permission, userId, request.KeyEnvelopeReference, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/groups/{groupId}/grants/{grant.Id}"),
            ResourceGrantResponse.Of(grant));
    }
}
