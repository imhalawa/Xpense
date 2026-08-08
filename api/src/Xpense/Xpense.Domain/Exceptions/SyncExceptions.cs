namespace Xpense.Domain.Exceptions;

public class InvalidSyncCursorException(Exception? innerException = null)
    : DomainRuleViolationException("The sync cursor is invalid", innerException);
