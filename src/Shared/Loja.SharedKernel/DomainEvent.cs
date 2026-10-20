namespace Loja.SharedKernel;

/// <summary>
/// Something that happened in the domain. Saved to the outbox and published after the transaction commits.
/// </summary>
public interface IDomainEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredOnUtc { get; }
}

/// <summary>
/// Base record for domain events, e.g. <c>public sealed record ProductCreated(ProductId ProductId) : DomainEvent;</c>
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
