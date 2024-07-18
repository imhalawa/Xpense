namespace Xpense.Services.Exceptions
{
    public class IncompaitableCurrencyOperationException(Exception? innerException = null)
    : XpenseException($"\"NotSupported: Cannot do arithmetic operations on money value objects of different currencies", innerException);
}
