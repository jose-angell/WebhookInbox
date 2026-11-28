using WebhookInbox.Application.Abstractions;
using WebhookInbox.Application.Dtos;
using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;
using WebhookInbox.Domain.Repositories;
using WebhookInbox.Domain.Services;

namespace WebhookInbox.Application.Services;

public sealed class InboxEventService(
    InboxEventRegistrar registrar,
    IInboxEventRepository repository,
    IInboxEventProcessor processor,
    TimeProvider timeProvider)
{
    private const int MaxPageSize = 100;

    public async Task<InboxEventDto> RegisterAsync(string eventId, string source, string eventType,
                                                   string payload, CancellationToken ct)
    {
        var inboxEvent = await registrar.RegisterAsync(eventId, source, eventType, payload, ct);
        return InboxEventDto.From(inboxEvent);
    }

    public async Task<InboxEventDto> ProcessAsync(string eventId, CancellationToken ct)
    {
        var inboxEvent = await repository.GetByEventIdAsync(eventId, ct)
                         ?? throw new EventNotFoundException(eventId);

        inboxEvent.StartProcessing();
        await repository.UpdateAsync(inboxEvent, ct);

        try
        {
            await processor.ProcessAsync(InboxEventDto.From(inboxEvent), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // MarkFailed rejects empty messages, so fall back to the exception type name.
            inboxEvent.MarkFailed(string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message);
            await repository.UpdateAsync(inboxEvent, ct);
            return InboxEventDto.From(inboxEvent);
        }

        inboxEvent.MarkProcessed(timeProvider.GetUtcNow());
        await repository.UpdateAsync(inboxEvent, ct);
        return InboxEventDto.From(inboxEvent);
    }

    public async Task<InboxEventDto> GetByEventIdAsync(string eventId, CancellationToken ct)
    {
        var inboxEvent = await repository.GetByEventIdAsync(eventId, ct)
                         ?? throw new EventNotFoundException(eventId);
        return InboxEventDto.From(inboxEvent);
    }

    public async Task<PagedResult<InboxEventDto>> ListAsync(EventStatus? status, int page, int pageSize,
                                                            CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var skip = (page - 1) * pageSize;
        var (items, total) = await repository.ListAsync(status, skip, pageSize, ct);

        return new PagedResult<InboxEventDto>(items.Select(InboxEventDto.From).ToList(), page, pageSize, total);
    }
}
