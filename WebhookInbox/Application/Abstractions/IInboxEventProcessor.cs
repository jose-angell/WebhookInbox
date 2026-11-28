using WebhookInbox.Application.Dtos;

namespace WebhookInbox.Application.Abstractions;

public interface IInboxEventProcessor
{
    Task ProcessAsync(InboxEventDto inboxEvent, CancellationToken ct);
}
