namespace Xpense.Domain.Exceptions;

public class MerchantNotFoundException : NotFoundException
{
    public MerchantNotFoundException(string label, Exception? innerException = null)
        : base($"The merchant ({label}) was neither found nor requested to be created.", innerException)
    {
    }

    public MerchantNotFoundException(int id, Exception? innerException = null)
        : base($"Merchant with id {id} was not found.", innerException)
    {
    }
}

public class MerchantCreationFailedException(string label, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to create Merchant ({label})", innerException);

public class MerchantUpdateFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed to update merchant with id {id}", innerException);

public class MerchantDeletionFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed to delete merchant with id {id}", innerException);
