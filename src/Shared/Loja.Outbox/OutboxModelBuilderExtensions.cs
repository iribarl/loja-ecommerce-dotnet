using Microsoft.EntityFrameworkCore;

namespace Loja.Outbox;

public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Adds the outbox and inbox tables to a module's model, in the module's default schema.
    /// Call after <c>HasDefaultSchema</c>.
    /// </summary>
    public static ModelBuilder AddOutboxAndInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.HasKey(m => m.Id);
            outbox.Property(m => m.Id).ValueGeneratedNever();
            outbox.Property(m => m.Type).HasMaxLength(500);
            outbox.Property(m => m.Content).HasColumnType("jsonb");
            outbox.Property(m => m.LastError).HasMaxLength(4000);

            // Partial index: only pending messages are searched, and they are few.
            outbox.HasIndex(m => m.OccurredOnUtc).HasFilter("\"ProcessedOnUtc\" IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable("inbox_messages");
            inbox.HasKey(m => new { m.MessageId, m.Consumer });
            inbox.Property(m => m.Consumer).HasMaxLength(300);
        });

        return modelBuilder;
    }
}
