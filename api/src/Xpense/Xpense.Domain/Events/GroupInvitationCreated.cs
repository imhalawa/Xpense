namespace Xpense.Domain.Events;

public sealed record GroupInvitationCreated(Guid InvitationId) : EventBody;
