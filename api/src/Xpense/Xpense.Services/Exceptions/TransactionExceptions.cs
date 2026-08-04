namespace Xpense.Services.Exceptions;

public class DepositCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : XpenseException($"Failed Attempt to deposit amount {amount} to account {accountNumber}", innerException);

public class WithdrawCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : XpenseException($"Failed Attempt to withdraw amount {amount} from account {accountNumber}", innerException);

/// <summary>
/// The caller named a transaction type the system does not accept. Only income and expense cross
/// the boundary; a transfer moves money between two accounts and has its own contract.
/// </summary>
public class UnsupportedTransactionTypeException(string? transactionType, Exception? innerException = null)
    : XpenseException(
        $"Transaction type '{transactionType}' is not supported. Use 'income' or 'expense'.",
        innerException);

/// <summary>
/// The caller named a currency the system does not hold. Xpense does not convert between
/// currencies, so an unknown one cannot be substituted for a known one.
/// </summary>
public class UnsupportedCurrencyException(string? currency, Exception? innerException = null)
    : XpenseException(
        $"Currency '{currency}' is not supported.",
        innerException);