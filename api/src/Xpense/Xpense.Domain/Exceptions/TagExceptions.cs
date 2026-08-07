namespace Xpense.Domain.Exceptions;

public class TagNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Tag with id {id} was not found!", innerException);

public class TagDeletionFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to remove tag {id}", innerException);

public class TagCreationFailedException(string name, Exception? innerException = null)
    : PersistenceFailedException($"Failed Attempt to create tag {name}", innerException);

public class TagUpdateFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed to update tag with id {id}", innerException);
