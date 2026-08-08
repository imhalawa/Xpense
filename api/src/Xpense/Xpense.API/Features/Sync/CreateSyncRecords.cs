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
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;
using Xpense.Domain.Exceptions;
using Xpense.Persistence;

namespace Xpense.API.Features.Sync;

public sealed class CreateSyncRecords : IEndpoint
{
    private const int MaxBatchSize = 100;
    private const int MaxCiphertextLength = 65536;
    private const int MaxEnvelopeLength = 4096;
    private const int MaxIdempotencyKeyLength = 200;
    private const int MaxNonceLength = 4096;
    private const int SupportedProtocolVersion = 1;

    /// <summary>A batch of encrypted records to create atomically.</summary>
    public sealed record Request(RecordRequest[] Records);

    /// <summary>One encrypted record and its personal key envelope.</summary>
    public sealed record RecordRequest(
        Guid Id,
        string IdempotencyKey,
        EncryptedRecordType RecordType,
        Guid? ParentResourceId,
        int ProtocolVersion,
        byte[] Nonce,
        byte[] Ciphertext,
        PersonalEnvelopeRequest PersonalEnvelope);

    /// <summary>The record key wrapped for its personal owner.</summary>
    public sealed record PersonalEnvelopeRequest(
        byte[] WrappedKey,
        byte[] Nonce,
        int ProtocolVersion);

    /// <summary>The created records in request order.</summary>
    public sealed record Response(EncryptedRecordResponse[] Records);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Records)
                .NotEmpty().WithMessage("At least one encrypted record is required.")
                .Must(records => records.Length <= MaxBatchSize)
                .WithMessage($"A batch cannot contain more than {MaxBatchSize} encrypted records.")
                .Must(HaveUniqueIds)
                .WithMessage("Every encrypted record id in a batch must be unique.")
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
            RuleFor(record => record.Id).NotEmpty().WithMessage("The encrypted record id is required.");
            RuleFor(record => record.IdempotencyKey)
                .NotEmpty().WithMessage("The idempotency key is required.")
                .MaximumLength(MaxIdempotencyKeyLength);
            RuleFor(record => record.RecordType)
                .IsInEnum().WithMessage("The encrypted record type must be a valid selection.");
            RuleFor(record => record.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The protocol version is not supported.");
            RuleFor(record => record.Nonce)
                .NotEmpty().WithMessage("The record nonce is required.")
                .Must(nonce => nonce.Length <= MaxNonceLength).WithMessage("The record nonce is too large.");
            RuleFor(record => record.Ciphertext)
                .NotEmpty().WithMessage("The record ciphertext is required.")
                .Must(ciphertext => ciphertext.Length <= MaxCiphertextLength)
                .WithMessage("The record ciphertext is too large.");
            RuleFor(record => record.PersonalEnvelope).SetValidator(new PersonalEnvelopeValidator());
        }
    }

    public sealed class PersonalEnvelopeValidator : AbstractValidator<PersonalEnvelopeRequest>
    {
        public PersonalEnvelopeValidator()
        {
            RuleFor(envelope => envelope.WrappedKey)
                .NotEmpty().WithMessage("The wrapped record key is required.")
                .Must(wrappedKey => wrappedKey.Length <= MaxEnvelopeLength)
                .WithMessage("The wrapped record key is too large.");
            RuleFor(envelope => envelope.Nonce)
                .NotEmpty().WithMessage("The envelope nonce is required.")
                .Must(nonce => nonce.Length <= MaxNonceLength).WithMessage("The envelope nonce is too large.");
            RuleFor(envelope => envelope.ProtocolVersion)
                .Equal(SupportedProtocolVersion).WithMessage("The envelope protocol version is not supported.");
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/sync/records", Handle)
            .WithName(nameof(CreateSyncRecords))
            .Validated();

    private static async Task<Created<Response>> Handle(
        Request request,
        XpenseDbContext dbContext,
        SyncAuthorization authorization,
        ICurrentUser currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var responses = new List<EncryptedRecordResponse>(request.Records.Length);

        foreach (var item in request.Records)
        {
            var existingOperation = await dbContext.SyncOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    operation => operation.UserId == currentUser.Id && operation.IdempotencyKey == item.IdempotencyKey,
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
                ProtocolVersion = item.ProtocolVersion,
                Nonce = item.Nonce,
                Ciphertext = item.Ciphertext,
                CreatedAt = now,
                UpdatedAt = now
            };
            var envelope = new RecordEnvelope
            {
                Id = Guid.CreateVersion7(),
                EncryptedRecordId = record.Id,
                WrappedKey = item.PersonalEnvelope.WrappedKey,
                Nonce = item.PersonalEnvelope.Nonce,
                ProtocolVersion = item.PersonalEnvelope.ProtocolVersion
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
            dbContext.RecordEnvelopes.Add(envelope);
            dbContext.SyncOperations.Add(operation);
            await dbContext.SaveChangesAsync(cancellationToken);
            responses.Add(EncryptedRecordResponse.Of(record, [RecordEnvelopeResponse.Of(envelope)]));
        }

        await transaction.CommitAsync(cancellationToken);
        var firstId = responses[0].Id;
        return TypedResults.Created(
            httpContext.ResourceUri($"/api/v1/sync/records/{firstId}"),
            new Response(responses.ToArray()));
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
        var envelope = await dbContext.RecordEnvelopes
            .AsNoTracking()
            .SingleAsync(
                item => item.EncryptedRecordId == encryptedRecordId && item.GroupId == null,
                cancellationToken);
        return EncryptedRecordResponse.Of(record, [RecordEnvelopeResponse.Of(envelope)]);
    }
}
