using Xpense.Domain.Models;

namespace Xpense.Domain.Exceptions;

public class InvalidFilteredResultParams(FilterQuery query, Exception? innerException = null)
    : DomainRuleViolationException(
        $"Invalid filtration params {nameof(query.Page)}:{query.Page}, {nameof(query.Size)}:{query.Size} must be greater than 0",
        innerException);
