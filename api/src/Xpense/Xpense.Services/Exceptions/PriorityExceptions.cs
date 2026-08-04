namespace Xpense.Services.Exceptions;

public class PriorityCreationFailedException(string name, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to create priority {name}", innerException);

public class PriorityDeletionFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to remove priority with id:[{id}]", innerException);

public class PriorityNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Priority with id:[{id}] was not found", innerException);

public class PriorityUpdateFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to update priority with id:[{id}]", innerException);
