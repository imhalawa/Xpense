namespace Xpense.Domain.Enums;

/// <summary>
/// Whether a transaction raised a balance from outside Xpense, lowered it to outside Xpense, or
/// moved money between two accounts inside it.
/// <para>
/// Never stored. It is derived from which of a transaction's two sides name an account, so a row
/// cannot claim a kind that contradicts its own columns. Replaces the stored
/// <c>TransactionType { Credit, Debit, Transfer }</c>, whose Credit and Debit carried the opposite
/// integer values to the deleted TransferLegDirection.
/// </para>
/// </summary>
public enum TransactionKind
{
    Income,
    Expense,
    Transfer
}
