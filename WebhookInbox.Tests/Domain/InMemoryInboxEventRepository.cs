using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Repositories;

namespace WebhookInbox.Tests.Domain;

public sealed class InMemoryInboxEventRepository : IInboxEventRepository
{
    private readonly List<InboxEvent> _events = [];

    public IReadOnlyList<InboxEvent> Events => _events;
    public int AddCallCount { get; private set; }

    public Task<bool> ExistsAsync(string eventId, CancellationToken ct) =>
        Task.FromResult(_events.Any(e => string.Equals(e.EventId, eventId, StringComparison.Ordinal)));

    public Task AddAsync(InboxEvent inboxEvent, CancellationToken ct)
    {
        AddCallCount++;
        _events.Add(inboxEvent);
        return Task.CompletedTask;
    }
}
