namespace Xpense.Domain.Exceptions;

public class TransactionNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Transaction with id:[{id}] was not found", innerException);

public class DepositCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to deposit amount {amount} to account {accountNumber}", innerException);

public class WithdrawCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to withdraw amount {amount} from account {accountNumber}", innerException);

public class InvalidTransferException(string message) : DomainRuleViolationException(message);

public class InsufficientFundsForTransferException(int accountId, decimal balance, decimal amount)
    : DomainRuleViolationException($"Account {accountId} has balance {balance} but transfer requires {amount}.");
