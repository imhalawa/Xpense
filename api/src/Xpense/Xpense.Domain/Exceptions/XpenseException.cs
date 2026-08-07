namespace Xpense.Domain.Exceptions;

public class XpenseException(string message, Exception? innerException = null) : Exception(message, innerException);

public abstract class NotFoundException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);

public abstract class PersistenceFailedException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);

public abstract class DomainRuleViolationException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);
