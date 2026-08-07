namespace Xpense.Domain.Exceptions;

public class InvalidFilteredResultParams(int page, int pageSize, Exception? innerException = null)
    : DomainRuleViolationException(
        $"Invalid filtration params page:{page}, pageSize:{pageSize} must be greater than 0",
        innerException);
