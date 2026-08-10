using System;

namespace Xpense.Domain.Exceptions;

public sealed class ResourceGrantAlreadyActiveException(Exception? innerException = null)
    : DomainRuleViolationException("An active grant already exists for this resource and group", innerException);
