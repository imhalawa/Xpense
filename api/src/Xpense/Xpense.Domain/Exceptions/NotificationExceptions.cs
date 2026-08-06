namespace Xpense.Domain.Exceptions;

public class NotificationNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Notification with id {id} was not found!", innerException);
