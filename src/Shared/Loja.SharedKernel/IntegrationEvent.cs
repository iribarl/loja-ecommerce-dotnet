namespace Loja.SharedKernel;

/// <summary>
/// Something a module tells the others. Unlike domain events (internal to a module), integration
/// events are public: they live in the module's Contracts, so their shape is a promise to consumers.
/// Delivered through the outbox, at least once: handlers must be idempotent.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Reacts to an integration event from another module.
/// </summary>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IntegrationEvent
{
    Task ConsumeAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Non-generic access to the domain events of any entity, for infrastructure (the outbox) to collect them.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
