using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;

namespace WebhookInbox.Application.Dtos;

public sealed record InboxEventDto(
    Guid Id,
    string EventId,
    string Source,
    string EventType,
    string Payload,
    EventStatus Status,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    int AttemptCount,
    string? LastError)
{
    public static InboxEventDto From(InboxEvent e) => new(
        e.Id,
        e.EventId,
        e.Source,
        e.EventType,
        e.Payload,
        e.Status,
        e.ReceivedAt,
        e.ProcessedAt,
        e.AttemptCount,
        e.LastError);
}
