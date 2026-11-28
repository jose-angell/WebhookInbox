using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;
using WebhookInbox.Domain.Repositories;

namespace WebhookInbox.Infrastructure;

public sealed class EfInboxEventRepository(AppDbContext db) : IInboxEventRepository
{
    private const string UniqueViolation = "23505";

    public Task<bool> ExistsAsync(string eventId, CancellationToken ct) =>
        db.InboxEvents.AnyAsync(e => e.EventId == eventId, ct);

    public async Task AddAsync(InboxEvent inboxEvent, CancellationToken ct)
    {
        db.InboxEvents.Add(inboxEvent);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            throw new DuplicateEventException(inboxEvent.EventId);
        }
    }

    public Task<InboxEvent?> GetByEventIdAsync(string eventId, CancellationToken ct) =>
        db.InboxEvents.FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public async Task<(IReadOnlyList<InboxEvent> Items, int Total)> ListAsync(
        EventStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.InboxEvents.AsNoTracking();

        if (status is not null)
            query = query.Where(e => e.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ReceivedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task UpdateAsync(InboxEvent inboxEvent, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(inboxEvent.EventId);
        }
    }
}
