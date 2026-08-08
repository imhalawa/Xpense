namespace Xpense.Domain.Exceptions;

public class InvitationInvalidException(Exception? innerException = null)
    : DomainRuleViolationException("The invitation is invalid or has expired", innerException);

public class InvitationStateConflictException(Exception? innerException = null)
    : DomainRuleViolationException("The invitation cannot be changed from its current state", innerException);
