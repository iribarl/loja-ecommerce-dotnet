using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Loja.Outbox;

/// <summary>
/// Translates a module's domain events into integration events for other modules.
/// Returns null for domain events that stay inside the module.
/// </summary>
public interface IIntegrationEventMapper
{
    IntegrationEvent? Map(IDomainEvent domainEvent);
}

/// <summary>
/// Just before SaveChanges, collects the domain events of the tracked entities, maps them to
/// integration events and adds them to the outbox table. EF saves both in one transaction.
/// </summary>
/// <remarks>Stateless, so one instance can serve every (pooled) DbContext.</remarks>
public sealed class OutboxInterceptor(IIntegrationEventMapper mapper) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteOutbox(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WriteOutbox(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteOutbox(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                if (mapper.Map(domainEvent) is { } integrationEvent)
                {
                    context.Set<OutboxMessage>().Add(new OutboxMessage
                    {
                        Id = integrationEvent.Id,
                        Type = IntegrationEventSerializer.TypeName(integrationEvent),
                        Content = IntegrationEventSerializer.Serialize(integrationEvent),
                        OccurredOnUtc = integrationEvent.OccurredOnUtc,
                    });
                }
            }

            entity.ClearDomainEvents();
        }
    }
}
