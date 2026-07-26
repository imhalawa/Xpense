using Xpense.Services.ValueObjects;

namespace Xpense.Services.Features.Transactions.Commands;

public sealed class TransferTransactionCommand(
    Money amount,
    int sourceAccountId,
    int destinationAccountId,
    string? reason = null,
    long? createdOn = null)
{
    public Money Amount { get; } = amount;
    public int SourceAccountId { get; } = sourceAccountId;
    public int DestinationAccountId { get; } = destinationAccountId;
    public string? Reason { get; } = reason;
    public long? CreatedOn { get; } = createdOn;
}
