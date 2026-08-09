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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class AddRecordEnvelope : IEndpoint
{
    private const int MaxEnvelopeLength = 4096;
    private const int MaxNonceLength = 4096;
    private const int SupportedProtocolVersion = 1;

    /// <summary>A group key envelope and the permission it grants.</summary>
    public sealed record Request(
        Guid GroupId,
        GrantPermission Permission,
        byte[] WrappedKey,
        byte[] Nonce,
        byte[]? EncapsulatedKey,
        int ProtocolVersion);

    /// <summary>The stored group key envelope.</summary>
    public sealed record Response(RecordEnvelopeResponse Envelope);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.GroupId)
                .NotEmpty().WithMessage("The group is required.");
            RuleFor(request => request.Permission)
                .IsInEnum().WithMessage("The grant permission must be a valid selection.");
            RuleFor(request => request.WrappedKey)
                .NotEmpty().WithMessage("The wrapped record key is required.")
                .Must(wrappedKey => wrappedKey.Length <= MaxEnvelopeLength)
                .WithMessage("The wrapped record key is too large.");
            RuleFor(request => request.Nonce)
                .NotEmpty().WithMessage("The envelope nonce is required.")
                .Must(nonce => nonce.Length <= MaxNonceLength)
                .WithMessage("The envelope nonce is too large.");
            RuleFor(request => request.EncapsulatedKey)
                .Must(encapsulatedKey => encapsulatedKey is null || encapsulatedKey.Length <= MaxEnvelopeLength)
                .WithMessage("The encapsulated key is too large.");
            RuleFor(request => request.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The envelope protocol version is not supported.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/sync/records/{id:guid}/envelopes", Handle)
            .WithName(nameof(AddRecordEnvelope))
            .Validated();

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        Guid id,
        Request request,
        XpenseDbContext dbContext,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var record = await dbContext.EncryptedRecords.SingleOrDefaultAsync(
            item => item.Id == id && item.OwnerUserId == currentUser.Id,
            cancellationToken);
        if (record?.ParentResourceId is not Guid resourceId ||
            !await IsActiveGroupMember(request.GroupId, currentUser.Id, dbContext, cancellationToken))
            return TypedResults.NotFound();

        var resource = await dbContext.SharedResources.SingleOrDefaultAsync(
            item => item.Id == resourceId && item.OwnerUserId == currentUser.Id,
            cancellationToken);
        if (resource is null)
            return TypedResults.NotFound();

        var envelope = await dbContext.RecordEnvelopes.SingleOrDefaultAsync(
            item => item.EncryptedRecordId == id && item.GroupId == request.GroupId,
            cancellationToken);
        if (envelope is null)
        {
            envelope = new RecordEnvelope
            {
                Id = Guid.CreateVersion7(),
                EncryptedRecordId = id,
                GroupId = request.GroupId
            };
            dbContext.RecordEnvelopes.Add(envelope);
        }

        envelope.WrappedKey = request.WrappedKey;
        envelope.Nonce = request.Nonce;
        envelope.EncapsulatedKey = request.EncapsulatedKey;
        envelope.ProtocolVersion = request.ProtocolVersion;

        var now = DateTime.UtcNow;
        var grant = await dbContext.ResourceGrants.SingleOrDefaultAsync(
            item =>
                item.GroupId == request.GroupId &&
                item.ResourceId == resourceId &&
                item.ResourceType == resource.Type &&
                item.State == GrantState.Active,
            cancellationToken);
        if (grant is null)
        {
            grant = await dbContext.ResourceGrants
                .Where(item =>
                    item.GroupId == request.GroupId &&
                    item.ResourceId == resourceId &&
                    item.ResourceType == resource.Type &&
                    item.State == GrantState.Revoked)
                .OrderByDescending(item => item.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (grant is null)
        {
            grant = new ResourceGrant
            {
                Id = Guid.CreateVersion7(),
                GroupId = request.GroupId,
                ResourceType = resource.Type,
                ResourceId = resourceId,
                GrantedByUserId = currentUser.Id,
                CreatedAt = now
            };
            dbContext.ResourceGrants.Add(grant);
        }

        grant.Permission = request.Permission;
        grant.State = GrantState.Active;
        grant.KeyEnvelopeReference = envelope.Id;
        grant.UpdatedAt = now;
        grant.RevokedAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(new Response(RecordEnvelopeResponse.Of(envelope)));
    }

    private static Task<bool> IsActiveGroupMember(
        Guid groupId,
        Guid userId,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken) =>
        dbContext.GroupMemberships.AnyAsync(
            membership =>
                membership.GroupId == groupId &&
                membership.UserId == userId &&
                membership.State == MembershipState.Active &&
                dbContext.Groups.Any(group => group.Id == groupId && !group.IsDeleted),
            cancellationToken);
}
