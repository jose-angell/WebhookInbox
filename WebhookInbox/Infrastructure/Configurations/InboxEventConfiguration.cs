using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebhookInbox.Domain.Entities;

namespace WebhookInbox.Infrastructure.Configurations;

public sealed class InboxEventConfiguration : IEntityTypeConfiguration<InboxEvent>
{
    public void Configure(EntityTypeBuilder<InboxEvent> builder)
    {
        builder.ToTable("inbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventId).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Payload).IsRequired().HasColumnType("text");
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();
        builder.Property(e => e.AttemptCount).IsRequired();
        builder.Property(e => e.LastError).HasColumnType("text");

        builder.HasIndex(e => e.EventId).IsUnique();

        // xmin: columna de sistema de Postgres usada como token de concurrencia (propiedad sombra).
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
