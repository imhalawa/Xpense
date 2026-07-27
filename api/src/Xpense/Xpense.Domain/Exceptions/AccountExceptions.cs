namespace Xpense.Domain.Exceptions;

public class AccountNotFoundException : NotFoundException
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

/// <summary>
/// The caller omitted an account and no default exists. This is a 400 rather than a 404:
/// nothing was looked up by identity, and the caller fixes it by naming an account.
/// </summary>
public class DefaultAccountNotFoundException(Exception? innerException = null)
    : DomainRuleViolationException("Account was not specified and no default account was found!", innerException);

public class AccountCreationFailedException(string name, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to create account {name}", innerException);

public class AccountUpdateFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to update account with id:[{id}]", innerException);

public class AccountDeletionFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to remove account with id:[{id}]", innerException);
