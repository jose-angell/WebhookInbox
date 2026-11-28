namespace WebhookInbox.Domain.Exceptions;

public sealed class EventNotFoundException(string eventId)
    : DomainException($"No event with EventId '{eventId}' was found.")
{
    public string EventId { get; } = eventId;
}
