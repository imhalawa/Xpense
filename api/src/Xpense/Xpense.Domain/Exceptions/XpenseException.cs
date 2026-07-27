namespace Xpense.Domain.Exceptions;

public class XpenseException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// A resource was looked up by identity and does not exist. Maps to 404.
/// </summary>
public abstract class NotFoundException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);

/// <summary>
/// A write we expected to succeed did not. The caller cannot fix this by changing the
/// request, so it maps to 500 rather than 4xx.
/// </summary>
public abstract class PersistenceFailedException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);

/// <summary>
/// The request was well-formed but breaks a domain rule. Maps to 400.
/// </summary>
public abstract class DomainRuleViolationException(string message, Exception? innerException = null)
    : XpenseException(message, innerException);
