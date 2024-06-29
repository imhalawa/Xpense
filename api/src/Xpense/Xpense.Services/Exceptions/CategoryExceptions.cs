namespace Xpense.Services.Exceptions;

public class CategoryCreationFailedException(string name, Exception? innerException = null)
    : XpenseException($"Failed Attempt to create category {name}", innerException);

public class CategoryDeletionFailedException(int id, Exception? innerException = null)
    : XpenseException($"Failed Attempt to remove category {id}", innerException);

public class CategoryNotFoundException(int id, Exception? innerException = null)
    : XpenseException($"Category with {id} was not found", innerException);