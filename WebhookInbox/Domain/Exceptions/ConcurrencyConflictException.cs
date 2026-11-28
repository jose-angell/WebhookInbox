namespace WebhookInbox.Domain.Exceptions;

public sealed class ConcurrencyConflictException(string eventId)
    : DomainException($"The event with EventId '{eventId}' was modified concurrently.")
{
    public string EventId { get; } = eventId;
}
