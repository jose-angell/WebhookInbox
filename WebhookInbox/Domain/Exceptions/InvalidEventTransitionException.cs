using WebhookInbox.Domain.Enums;

namespace WebhookInbox.Domain.Exceptions;

public sealed class InvalidEventTransitionException(EventStatus currentStatus, string attemptedOperation)
    : DomainException($"Cannot perform '{attemptedOperation}' on an event in status '{currentStatus}'.")
{
    public EventStatus CurrentStatus { get; } = currentStatus;
    public string AttemptedOperation { get; } = attemptedOperation;
}
