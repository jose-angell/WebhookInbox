using WebhookInbox.Application.Abstractions;
using WebhookInbox.Application.Dtos;

namespace WebhookInbox.Tests.Application;

public sealed class FakeInboxEventProcessor : IInboxEventProcessor
{
    public Exception? ExceptionToThrow { get; set; }
    public int CallCount { get; private set; }
    public InboxEventDto? LastReceived { get; private set; }

    public Task ProcessAsync(InboxEventDto inboxEvent, CancellationToken ct)
    {
        CallCount++;
        LastReceived = inboxEvent;

        return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
    }
}
