using Loja.Outbox;
using Loja.Payments.Domain;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Loja.Payments.Infrastructure.Persistence;

/// <summary>
/// The Payments module's own database context, in the "payments" schema, with its outbox.
/// </summary>
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary>Webhook deliveries already handled (idempotency).</summary>
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();

    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Payment>(payment =>
        {
            payment.ToTable("payments");
            payment.HasKey(p => p.Id);
            payment.Property(p => p.Id).HasConversion(id => id.Value, value => new PaymentId(value)).ValueGeneratedNever();
            payment.Property(p => p.Gateway).HasMaxLength(30);
            payment.Property(p => p.ExternalId).HasMaxLength(100);
            payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            payment.Property(p => p.PixCopyPaste).HasMaxLength(1000);
            payment.ComplexProperty(p => p.Amount, amount =>
            {
                amount.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(12, 2);
                amount.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsFixedLength();
            });

            payment.HasIndex(p => p.OrderId);
            payment.HasIndex(p => new { p.Gateway, p.ExternalId }).IsUnique();
            payment.HasIndex(p => p.ExpiresAt).HasFilter("\"Status\" = 'Pending'");

            // The webhook and the expiry job may touch the same payment at the same moment.
            payment.Property<uint>("Version").IsRowVersion();
            payment.Ignore(p => p.DomainEvents);
        });

        modelBuilder.Entity<ProcessedWebhook>(webhook =>
        {
            webhook.ToTable("processed_webhooks");
            webhook.HasKey(w => new { w.Gateway, w.EventId });
            webhook.Property(w => w.Gateway).HasMaxLength(30);
            webhook.Property(w => w.EventId).HasMaxLength(200);
        });

        modelBuilder.AddOutboxAndInbox();
    }
}

/// <summary>A webhook delivery already handled: the same (gateway, event id) again is ignored.</summary>
public sealed class ProcessedWebhook
{
    public required string Gateway { get; init; }

    public required string EventId { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }
}

/// <summary>Used only by the dotnet-ef tool to create migrations; the connection string is a placeholder.</summary>
internal sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql("Host=design-time-only", PaymentsDbContext.ConfigureNpgsql)
            .Options);
}
