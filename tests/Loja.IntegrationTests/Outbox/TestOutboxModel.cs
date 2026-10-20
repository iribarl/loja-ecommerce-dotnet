using System.Collections.Concurrent;
using Loja.Outbox;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Outbox;

// A minimal "module" to test the outbox on its own: an entity that raises a domain event,
// a mapper to an integration event, and a consumer that records what it receives.

public sealed class Parcel : Entity<Guid>
{
    public Parcel(Guid id)
        : base(id)
    {
    }

    public string Status { get; private set; } = "new";

    public void Ship()
    {
        Status = "shipped";
        Raise(new ParcelShipped(Id));
    }
}

public sealed record ParcelShipped(Guid ParcelId) : DomainEvent;

public sealed record ParcelShippedIntegrationEvent(Guid ParcelId) : IntegrationEvent;

internal sealed class ParcelMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        ParcelShipped shipped => new ParcelShippedIntegrationEvent(shipped.ParcelId),
        _ => null,
    };
}

internal sealed class TestOutboxDbContext(DbContextOptions<TestOutboxDbContext> options) : DbContext(options)
{
    public const string Schema = "outbox_tests";

    public DbSet<Parcel> Parcels => Set<Parcel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Parcel>(parcel =>
        {
            parcel.HasKey(p => p.Id);
            parcel.Property(p => p.Id).ValueGeneratedNever();
            parcel.Ignore(p => p.DomainEvents);
        });
        modelBuilder.AddOutboxAndInbox();
    }
}

/// <summary>Records deliveries; can be told to fail for a given parcel.</summary>
internal sealed class RecordingConsumer : IIntegrationEventConsumer<ParcelShippedIntegrationEvent>
{
    public static ConcurrentBag<Guid> Received { get; } = [];

    public static ConcurrentDictionary<Guid, bool> FailFor { get; } = [];

    public Task ConsumeAsync(ParcelShippedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (FailFor.ContainsKey(integrationEvent.ParcelId))
        {
            throw new InvalidOperationException("Simulated consumer failure.");
        }

        Received.Add(integrationEvent.ParcelId);
        return Task.CompletedTask;
    }
}
