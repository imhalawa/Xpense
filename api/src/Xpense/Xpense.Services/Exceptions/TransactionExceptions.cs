namespace Xpense.Services.Exceptions;

public class DepositCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : XpenseException($"Failed Attempt to deposit amount {amount} to account {accountNumber}", innerException);

public class WithdrawCreationFailedException(decimal amount, string accountNumber, Exception? innerException = null)
    : XpenseException($"Failed Attempt to withdraw amount {amount} from account {accountNumber}", innerException);