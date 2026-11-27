using WebhookInbox.Domain.Entities;

namespace WebhookInbox.Domain.Repositories;

public interface IInboxEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken ct);
    Task AddAsync(InboxEvent inboxEvent, CancellationToken ct);
}
