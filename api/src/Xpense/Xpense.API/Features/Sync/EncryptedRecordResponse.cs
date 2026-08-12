using System;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Sync;

/// <summary>A synchronised record and its JSON payload.</summary>
public sealed record EncryptedRecordResponse(
    Guid Id,
    EncryptedRecordType RecordType,
    Guid OwnerId,
    Guid? ParentResourceId,
    long Revision,
    byte[] Payload,
    bool Tombstone,
    long SequenceNumber,
    DateTime ServerCreatedAt,
    DateTime ServerUpdatedAt)
{
    public static EncryptedRecordResponse Of(EncryptedRecord record) => new(
        record.Id,
        record.RecordType,
        record.OwnerUserId,
        record.ParentResourceId,
        record.Revision,
        record.Payload,
        record.IsDeleted,
        record.SequenceNumber,
        record.CreatedAt,
        record.UpdatedAt);
}

/// <summary>A sequence-ordered page of accessible changes.</summary>
public sealed record SyncChangesResponse(
    EncryptedRecordResponse[] Records,
    string NextCursor,
    bool HasMore);
