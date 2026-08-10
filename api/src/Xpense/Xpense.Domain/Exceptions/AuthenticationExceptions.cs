namespace Xpense.Domain.Exceptions;

public sealed class PasskeySignInInvalidException()
    : XpenseException("The passkey sign-in request is invalid or has expired.");

public sealed class PasskeyManagementInvalidException()
    : DomainRuleViolationException("The passkey registration request is invalid or has expired.");
