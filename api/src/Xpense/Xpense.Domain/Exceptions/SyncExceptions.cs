namespace Xpense.Domain.Exceptions;

public class InvalidSyncCursorException(Exception? innerException = null)
    : DomainRuleViolationException("The sync cursor is invalid", innerException);

public class SyncResourceNotFoundException(Guid id, Exception? innerException = null)
    : NotFoundException($"Encrypted resource {id} was not found", innerException);
