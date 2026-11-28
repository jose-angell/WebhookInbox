using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;
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

    public int UpdateCallCount { get; private set; }

    public Task<InboxEvent?> GetByEventIdAsync(string eventId, CancellationToken ct) =>
        Task.FromResult(_events.FirstOrDefault(e => string.Equals(e.EventId, eventId, StringComparison.Ordinal)));

    public Task<(IReadOnlyList<InboxEvent> Items, int Total)> ListAsync(
        EventStatus? status, int skip, int take, CancellationToken ct)
    {
        var filtered = _events
            .Where(e => status is null || e.Status == status)
            .OrderByDescending(e => e.ReceivedAt)
            .ToList();

        IReadOnlyList<InboxEvent> items = filtered.Skip(skip).Take(take).ToList();
        return Task.FromResult((items, filtered.Count));
    }

    public Task UpdateAsync(InboxEvent inboxEvent, CancellationToken ct)
    {
        UpdateCallCount++;
        return Task.CompletedTask;
    }
}
