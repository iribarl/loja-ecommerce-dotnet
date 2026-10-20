using Loja.Catalog.Contracts;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Orders.Contracts;
using Loja.Outbox;
using Loja.SharedKernel;

namespace Loja.Catalog.Infrastructure.Consumers;

/// <summary>
/// A cancelled order (by the customer, or because its payment expired) gives its units back.
/// </summary>
/// <remarks>
/// Releasing is idempotent on its own (only open reservations change); the inbox also records the
/// message, so a redelivery is skipped without touching the stock tables and the processing is traceable.
/// </remarks>
internal sealed class ReleaseStockWhenOrderCancelled(
    CatalogDbContext db,
    IStockReservations reservations,
    TimeProvider clock) : IIntegrationEventConsumer<OrderCancelledIntegrationEvent>
{
    private const string Name = "catalog.release-stock-when-order-cancelled";

    public async Task ConsumeAsync(OrderCancelledIntegrationEvent message, CancellationToken cancellationToken)
    {
        if (await db.HasProcessedAsync(message.Id, Name, cancellationToken))
        {
            return;
        }

        await reservations.ReleaseAsync(message.OrderId, cancellationToken);

        db.MarkProcessed(message.Id, Name, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}
