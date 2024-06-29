namespace Xpense.Services.Exceptions;

public class XpenseException(string message, Exception? innerException = null) : Exception(message, innerException);