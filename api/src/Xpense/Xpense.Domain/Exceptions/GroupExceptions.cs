namespace Xpense.Domain.Exceptions;

public class GroupNotFoundException(Guid id, Exception? innerException = null)
    : NotFoundException($"Group with id {id} was not found", innerException);

public class GroupOwnerCannotLeaveException(Guid id, Exception? innerException = null)
    : DomainRuleViolationException($"The owner must transfer or delete group {id} before leaving", innerException);

public class GroupNotEmptyException(Guid id, Exception? innerException = null)
    : DomainRuleViolationException($"Group {id} cannot be deleted while another active member belongs to it", innerException);

public class LastVaultWrapperException(Exception? innerException = null)
    : DomainRuleViolationException("The final vault recovery method cannot be removed", innerException);
