using Xpense.Domain.Enums;

namespace Xpense.Domain.Events;

public sealed record TransactionRecorded(
    int TransactionId,
    TransactionKind Kind,
    long AmountMinorUnits,
    Currency Currency,
    DateTime OccurredAt,
    int? CategoryId,
    int? MerchantId,
    string? SourceAccountNumber,
    long? SourceBalanceAfterMinorUnits,
    string? DestinationAccountNumber,
    long? DestinationBalanceAfterMinorUnits) : EventBody;
