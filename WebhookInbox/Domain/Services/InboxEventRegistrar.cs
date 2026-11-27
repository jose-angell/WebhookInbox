using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Exceptions;
using WebhookInbox.Domain.Repositories;

namespace WebhookInbox.Domain.Services;

public sealed class InboxEventRegistrar(IInboxEventRepository repository, TimeProvider timeProvider)
{
    public async Task<InboxEvent> RegisterAsync(string eventId, string source, string eventType,
                                                string payload, CancellationToken ct)
    {
        var inboxEvent = InboxEvent.Receive(eventId, source, eventType, payload, timeProvider.GetUtcNow());

        if (await repository.ExistsAsync(eventId, ct))
            throw new DuplicateEventException(eventId);

        await repository.AddAsync(inboxEvent, ct);
        return inboxEvent;
    }
}
