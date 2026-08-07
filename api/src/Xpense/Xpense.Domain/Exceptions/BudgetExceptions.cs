namespace Xpense.Domain.Exceptions;

public class InvalidBudgetException(string message, Exception? innerException = null)
    : DomainRuleViolationException(message, innerException);

public class BudgetNotFoundException(int id, Exception? innerException = null)
    : NotFoundException($"Budget with id {id} was not found!", innerException);

public class BudgetCreationFailedException(int categoryId, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to create a budget for category {categoryId}", innerException);

public class BudgetUpdateFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed to update budget with id {id}", innerException);

public class BudgetDeletionFailedException(int id, Exception? innerException = null)
    : PersistenceFailedException($"Failed attempt to remove budget {id}", innerException);
