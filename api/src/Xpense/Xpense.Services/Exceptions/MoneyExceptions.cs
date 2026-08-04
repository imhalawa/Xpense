namespace Xpense.Services.Exceptions;

public class IncompatibleCurrencyOperationException(Exception? innerException = null)
    : DomainRuleViolationException(
        "Cannot do arithmetic operations on money value objects of different currencies",
        innerException);
