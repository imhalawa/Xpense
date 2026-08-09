namespace Xpense.Domain.Exceptions;

public sealed class PasskeySignInInvalidException()
    : XpenseException("The passkey sign-in request is invalid or has expired.");
