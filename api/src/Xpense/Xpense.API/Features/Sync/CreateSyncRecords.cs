using System;
using System.Collections.Generic;
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

namespace Xpense.API.Features.Sync;

public sealed class CreateSyncRecords : IEndpoint
{
    private const int MaxBatchSize = 100;
    private const int MaxIdempotencyKeyLength = 200;
    private const int MaxPayloadLength = 65536;

    /// <summary>A batch of records to create atomically.</summary>
    public sealed record Request(RecordRequest[] Records);

    /// <summary>One record and the JSON payload it carries.</summary>
    public sealed record RecordRequest(
        Guid Id,
        string IdempotencyKey,
        EncryptedRecordType RecordType,
        Guid? ParentResourceId,
        byte[] Payload);

    /// <summary>The created records in request order.</summary>
    public sealed record Response(EncryptedRecordResponse[] Records);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Records)
                .NotEmpty().WithMessage("At least one record is required.")
                .Must(records => records.Length <= MaxBatchSize)
                .WithMessage($"A batch cannot contain more than {MaxBatchSize} records.")
                .Must(HaveUniqueIds)
                .WithMessage("Every record id in a batch must be unique.")
                .Must(HaveUniqueIdempotencyKeys)
                .WithMessage("Every idempotency key in a batch must be unique.");

            RuleForEach(request => request.Records).SetValidator(new RecordValidator());
        }

        private static bool HaveUniqueIds(RecordRequest[] records) =>
            records.Select(record => record.Id).Distinct().Count() == records.Length;

        private static bool HaveUniqueIdempotencyKeys(RecordRequest[] records) =>
            records.Select(record => record.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() == records.Length;
    }

    public sealed class RecordValidator : AbstractValidator<RecordRequest>
    {
        public RecordValidator()
        {
            RuleFor(record => record.Id).NotEmpty().WithMessage("The record id is required.");
            RuleFor(record => record.IdempotencyKey)
                .NotEmpty().WithMessage("The idempotency key is required.")
                .MaximumLength(MaxIdempotencyKeyLength);
            RuleFor(record => record.RecordType)
                .IsInEnum().WithMessage("The record type must be a valid selection.");
            RuleFor(record => record.Payload)
                .NotEmpty().WithMessage("The record payload is required.")
                .Must(payload => payload.Length <= MaxPayloadLength)
                .WithMessage("The record payload is too large.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/sync/records", Handle)
            .WithName(nameof(CreateSyncRecords))
            .Validated();

    private static async Task<Created<Response>> Handle(
        Request request,
        ResourceTransactionLock resourceTransactionLock,
        XpenseDbContext dbContext,
        SyncAuthorization authorization,
        ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var rootResourceIds = request.Records
            .Where(item =>
                item.ParentResourceId == item.Id &&
                item.RecordType is EncryptedRecordType.Account or EncryptedRecordType.Budget)
            .Select(item => item.Id)
            .Distinct()
            .OrderBy(resourceId => resourceId)
            .ToArray();
        var resourceLocks = new List<ResourceTransactionLock.Lease>(rootResourceIds.Length);
        try
        {
            foreach (var resourceId in rootResourceIds)
                resourceLocks.Add(await resourceTransactionLock.Acquire(resourceId, cancellationToken));

            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var responses = new List<EncryptedRecordResponse>(request.Records.Length);

            foreach (var item in request.Records)
            {
                var existingOperation = await dbContext.SyncOperations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        operation =>
                            operation.UserId == currentUser.Id &&
                            operation.IdempotencyKey == item.IdempotencyKey,
                        cancellationToken);

                if (existingOperation is not null)
                {
                    responses.Add(await ReadExisting(
                        existingOperation.EncryptedRecordId,
                        dbContext,
                        cancellationToken));
                    continue;
                }

                await EnsureParentIsWritable(item, dbContext, authorization, currentUser.Id, cancellationToken);
                var now = DateTime.UtcNow;
                var record = new EncryptedRecord
                {
                    Id = item.Id,
                    RecordType = item.RecordType,
                    OwnerUserId = currentUser.Id,
                    ParentResourceId = item.ParentResourceId,
                    Revision = 1,
                    Payload = item.Payload,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var operation = new SyncOperation
                {
                    Id = Guid.CreateVersion7(),
                    UserId = currentUser.Id,
                    IdempotencyKey = item.IdempotencyKey,
                    EncryptedRecordId = record.Id,
                    CreatedAt = now
                };

                dbContext.EncryptedRecords.Add(record);
                dbContext.SyncOperations.Add(operation);
                await dbContext.SaveChangesAsync(cancellationToken);
                responses.Add(EncryptedRecordResponse.Of(record));
            }

            await transaction.CommitAsync(cancellationToken);
            var firstId = responses[0].Id;
            return TypedResults.Created(
                httpContext.ResourceUri($"/api/v1/sync/records/{firstId}"),
                new Response(responses.ToArray()));
        }
        finally
        {
            for (var index = resourceLocks.Count - 1; index >= 0; index--)
                await resourceLocks[index].DisposeAsync();
        }
    }

    private static async Task EnsureParentIsWritable(
        RecordRequest item,
        XpenseDbContext dbContext,
        SyncAuthorization authorization,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!item.ParentResourceId.HasValue)
            return;

        var parentResourceId = item.ParentResourceId.Value;
        if (parentResourceId == item.Id && TryResourceType(item.RecordType, out var resourceType))
        {
            var existing = await dbContext.SharedResources
                .AsNoTracking()
                .SingleOrDefaultAsync(resource => resource.Id == parentResourceId, cancellationToken);

            if (existing is null)
            {
                dbContext.SharedResources.Add(new SharedResource
                {
                    Id = parentResourceId,
                    Type = resourceType,
                    OwnerUserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
                return;
            }

            if (existing.OwnerUserId == userId && existing.Type == resourceType)
                return;
        }

        if (!await authorization.CanWriteResource(parentResourceId, cancellationToken))
            throw new SyncResourceNotFoundException(parentResourceId);
    }

    private static bool TryResourceType(EncryptedRecordType recordType, out SharedResourceType resourceType)
    {
        resourceType = recordType switch
        {
            EncryptedRecordType.Account => SharedResourceType.Account,
            EncryptedRecordType.Budget => SharedResourceType.Budget,
            _ => default
        };
        return recordType is EncryptedRecordType.Account or EncryptedRecordType.Budget;
    }

    private static async Task<EncryptedRecordResponse> ReadExisting(
        Guid encryptedRecordId,
        XpenseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.EncryptedRecords
            .AsNoTracking()
            .SingleAsync(item => item.Id == encryptedRecordId, cancellationToken);
        return EncryptedRecordResponse.Of(record);
    }
}
