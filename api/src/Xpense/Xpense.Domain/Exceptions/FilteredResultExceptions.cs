namespace Xpense.Domain.Exceptions;

public class InvalidFilteredResultParams(int page, int pageSize, Exception? innerException = null)
    : DomainRuleViolationException(
        $"Invalid filtration params page:{page}, pageSize:{pageSize} must be greater than 0",
        innerException);

public class InvalidFilteredResultRange(DateTimeOffset from, DateTimeOffset to, Exception? innerException = null)
    : DomainRuleViolationException(
        $"Invalid filtration range from:{from:o} must be earlier than to:{to:o}",
        innerException);
