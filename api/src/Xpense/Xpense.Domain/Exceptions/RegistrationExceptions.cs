namespace Xpense.Domain.Exceptions;

public sealed class RegistrationChallengeInvalidException(Exception? innerException = null)
    : DomainRuleViolationException("The registration request is invalid or has expired.", innerException);

public sealed class RegistrationNotAllowedException()
    : DomainRuleViolationException("Registration is not available.");
