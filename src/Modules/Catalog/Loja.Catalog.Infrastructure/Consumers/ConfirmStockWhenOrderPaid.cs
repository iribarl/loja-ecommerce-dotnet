using Loja.Catalog.Contracts;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Orders.Contracts;
using Loja.Outbox;
using Loja.SharedKernel;

namespace Loja.Catalog.Infrastructure.Consumers;

/// <summary>
/// A paid order makes its reserved units sold for good: they can no longer be released
/// (e.g. by the expired reservations sweeper).
/// </summary>
internal sealed class ConfirmStockWhenOrderPaid(
    CatalogDbContext db,
    IStockReservations reservations,
    TimeProvider clock) : IIntegrationEventConsumer<OrderPaidIntegrationEvent>
{
    private const string Name = "catalog.confirm-stock-when-order-paid";

    public async Task ConsumeAsync(OrderPaidIntegrationEvent message, CancellationToken cancellationToken)
    {
        if (await db.HasProcessedAsync(message.Id, Name, cancellationToken))
        {
            return;
        }

        await reservations.ConfirmAsync(message.OrderId, cancellationToken);

        db.MarkProcessed(message.Id, Name, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}
