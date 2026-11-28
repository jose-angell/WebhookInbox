using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;

namespace WebhookInbox.Domain.Repositories;

public interface IInboxEventRepository
{
    Task<bool> ExistsAsync(string eventId, CancellationToken ct);
    Task AddAsync(InboxEvent inboxEvent, CancellationToken ct);
    Task<InboxEvent?> GetByEventIdAsync(string eventId, CancellationToken ct);
    Task<(IReadOnlyList<InboxEvent> Items, int Total)> ListAsync(EventStatus? status, int skip, int take, CancellationToken ct);
    Task UpdateAsync(InboxEvent inboxEvent, CancellationToken ct);
}
