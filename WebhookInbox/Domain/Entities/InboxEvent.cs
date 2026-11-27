using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;

namespace WebhookInbox.Domain.Entities;

public sealed class InboxEvent
{
    public Guid Id { get; private set; }
    public string EventId { get; private set; }
    public string Source { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public EventStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    private InboxEvent(string eventId, string source, string eventType, string payload, DateTimeOffset receivedAt)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        Source = source;
        EventType = eventType;
        Payload = payload;
        Status = EventStatus.Received;
        ReceivedAt = receivedAt;
        AttemptCount = 0;
    }

    public static InboxEvent Receive(string eventId, string source, string eventType,
                                     string payload, DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventId);
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        return new InboxEvent(eventId, source, eventType, payload, receivedAt);
    }

    public void StartProcessing()
    {
        if (Status is not (EventStatus.Received or EventStatus.Failed))
            throw new InvalidEventTransitionException(Status, nameof(StartProcessing));

        Status = EventStatus.Processing;
        AttemptCount++;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        if (Status != EventStatus.Processing)
            throw new InvalidEventTransitionException(Status, nameof(MarkProcessed));

        Status = EventStatus.Processed;
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        if (Status != EventStatus.Processing)
            throw new InvalidEventTransitionException(Status, nameof(MarkFailed));

        Status = EventStatus.Failed;
        LastError = error;
    }
}
