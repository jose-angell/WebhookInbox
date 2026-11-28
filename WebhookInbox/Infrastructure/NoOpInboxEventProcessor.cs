using WebhookInbox.Application.Abstractions;
using WebhookInbox.Application.Dtos;

namespace WebhookInbox.Infrastructure;

public sealed class NoOpInboxEventProcessor : IInboxEventProcessor
{
    public Task ProcessAsync(InboxEventDto inboxEvent, CancellationToken ct) => Task.CompletedTask;
}
