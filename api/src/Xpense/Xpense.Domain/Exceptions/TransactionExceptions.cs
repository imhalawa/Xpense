namespace Xpense.Domain.Exceptions;

public class TransactionNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Transaction with id:[{id}] was not found", innerException);

public class DepositCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to deposit amount {amount} to account {accountNumber}", innerException);

public class WithdrawCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to withdraw amount {amount} from account {accountNumber}", innerException);

/// <summary>
/// A transaction was asked for that cannot exist: a non-positive amount, or a transfer whose two
/// accounts are the same or are denominated in different currencies.
/// </summary>
public class InvalidTransactionException(string message) : DomainRuleViolationException(message);

public class InsufficientFundsForTransferException(int accountId, object balance, object amount)
    : DomainRuleViolationException($"Account {accountId} has balance {balance} but transfer requires {amount}.");
