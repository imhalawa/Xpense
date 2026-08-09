namespace Xpense.Domain.Exceptions;

public sealed class LegacyClaimInvalidException(Exception? innerException = null)
    : DomainRuleViolationException("The legacy claim is unavailable", innerException);

public sealed class LegacyClaimVerificationFailedException(Exception? innerException = null)
    : DomainRuleViolationException("The legacy claim could not be verified", innerException);

public sealed class LegacyClaimAlreadyCompletedException(Exception? innerException = null)
    : DomainRuleViolationException("The legacy claim was already completed", innerException);

public sealed class LegacyClaimInProgressException(Exception? innerException = null)
    : DomainRuleViolationException("Legacy financial data is read-only while a claim is in progress", innerException);
