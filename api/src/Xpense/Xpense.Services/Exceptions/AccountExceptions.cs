namespace Xpense.Services.Exceptions;

public class AccountNotFoundException : XpenseException
{
    public AccountNotFoundException(string accountNumber, Exception? innerException = null)
        : base($"Account with number {accountNumber} was not found!", innerException)
    {
    }

    public AccountNotFoundException(int id, Exception? innerException = null)
        : base($"Account with id {id} was not found!", innerException)
    {
    }
}

public class DefaultAccountNotFoundException(Exception? innerException = null)
    : XpenseException($"Account was not specified and no default account was found! ", innerException);

public class AccountCreationFailedException(string name, Exception? innerException = null)
    : XpenseException($"Failed Attempt to create account {name}", innerException);

public class AccountUpdateFailedException(string accountNumber, Exception? innerException = null)
    : XpenseException($"Failed to update account with number {accountNumber}", innerException);
