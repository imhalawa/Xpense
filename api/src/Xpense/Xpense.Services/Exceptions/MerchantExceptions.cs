namespace Xpense.Services.Exceptions;

public class MerchantNotFoundException(string label, Exception? innerException = null)
    : XpenseException($"The Merchant ({label}) was neither found nor requested to be created!", innerException);

public class MerchantCreationFailedException(string label, Exception? innerException = null)
    : XpenseException($"Failed Attempt to create Merchant ({label})", innerException);

public class MerchantUpdateFailedException(string label, Exception? innerException = null)
    : XpenseException($"Failed to update Merchant ({label})", innerException);