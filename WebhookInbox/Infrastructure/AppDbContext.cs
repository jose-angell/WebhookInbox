using Microsoft.EntityFrameworkCore;
using WebhookInbox.Domain.Entities;

namespace WebhookInbox.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<InboxEvent> InboxEvents => Set<InboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
