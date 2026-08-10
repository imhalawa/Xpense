using System;
using System.Collections.Generic;
using Xpense.Domain.Enums;

namespace Xpense.API.Contracts;

public sealed record LegacyClaimDatasetResponse(
    int ProtocolVersion,
    int RecordCount,
    IReadOnlyDictionary<string, int> Counts,
    string ManifestHash,
    LegacyAccountRecord[] Accounts,
    LegacyNecessityScaleRecord[] NecessityScales,
    LegacyCategoryRecord[] Categories,
    LegacyMerchantRecord[] Merchants,
    LegacyTagRecord[] Tags,
    LegacyTransactionRecord[] Transactions,
    LegacyBudgetRecord[] Budgets,
    LegacyNotificationRecord[] Notifications);

public sealed record LegacyAccountRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Label,
    string AccountNumber,
    long BalanceMinorUnits,
    Currency Currency,
    bool IsDefault);

public sealed record LegacyNecessityScaleRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Label,
    double Weight);

public sealed record LegacyCategoryRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Label,
    int NecessityScaleLegacyId,
    Guid NecessityScaleId);

public sealed record LegacyMerchantRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Label);

public sealed record LegacyTagRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Label,
    string? BackgroundColorHex,
    string? ForegroundColorHex);

public sealed record LegacyTransactionRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    TransactionKind Kind,
    long AmountMinorUnits,
    Currency Currency,
    DateTime OccurredAt,
    string? Reason,
    int? SourceAccountLegacyId,
    Guid? SourceAccountId,
    int? DestinationAccountLegacyId,
    Guid? DestinationAccountId,
    int? CategoryLegacyId,
    Guid? CategoryId,
    int? MerchantLegacyId,
    Guid? MerchantId,
    int[] TagLegacyIds,
    Guid[] TagIds);

public sealed record LegacyBudgetRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    long AmountMinorUnits,
    Currency Currency,
    int CategoryLegacyId,
    Guid CategoryId,
    Recurrence Recurrence,
    DateTime StartsOn,
    DateTime? EndsOn,
    int? AlertThresholdPercent);

public sealed record LegacyNotificationRecord(
    Guid Id,
    int LegacyId,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int? OwnerLegacyId,
    Guid EventId,
    NotificationKind Kind,
    string Title,
    string Message,
    string Payload,
    string PayloadHash,
    DateTime? ReadAt);
