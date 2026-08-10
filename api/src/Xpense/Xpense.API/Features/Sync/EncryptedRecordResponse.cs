using System;
using Xpense.Domain.Entities;
using Xpense.Domain.Enums;

namespace Xpense.API.Features.Sync;

/// <summary>An encrypted record and the key envelopes the current user may receive.</summary>
public sealed record EncryptedRecordResponse(
    Guid Id,
    EncryptedRecordType RecordType,
    Guid OwnerUserId,
    Guid? ParentResourceId,
    long Revision,
    int ProtocolVersion,
    byte[] Nonce,
    byte[] Ciphertext,
    RecordEnvelopeResponse[] Envelopes,
    bool IsDeleted,
    long SequenceNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static EncryptedRecordResponse Of(EncryptedRecord record, RecordEnvelopeResponse[] envelopes) => new(
        record.Id,
        record.RecordType,
        record.OwnerUserId,
        record.ParentResourceId,
        record.Revision,
        record.ProtocolVersion,
        record.Nonce,
        record.Ciphertext,
        envelopes,
        record.IsDeleted,
        record.SequenceNumber,
        record.CreatedAt,
        record.UpdatedAt);
}

/// <summary>An opaque record key wrapped for the owner or one group.</summary>
public sealed record RecordEnvelopeResponse(
    Guid Id,
    Guid? GroupId,
    byte[] WrappedKey,
    byte[] Nonce,
    byte[]? EncapsulatedKey,
    int ProtocolVersion)
{
    public static RecordEnvelopeResponse Of(RecordEnvelope envelope) => new(
        envelope.Id,
        envelope.GroupId,
        envelope.WrappedKey,
        envelope.Nonce,
        envelope.EncapsulatedKey,
        envelope.ProtocolVersion);
}

/// <summary>A sequence-ordered page of accessible encrypted changes.</summary>
public sealed record SyncChangesResponse(
    EncryptedRecordResponse[] Records,
    string NextCursor,
    bool HasMore);
