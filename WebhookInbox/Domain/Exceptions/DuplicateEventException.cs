namespace WebhookInbox.Domain.Exceptions;

public sealed class DuplicateEventException(string eventId)
    : DomainException($"An event with EventId '{eventId}' is already registered.")
{
    public string EventId { get; } = eventId;
}
