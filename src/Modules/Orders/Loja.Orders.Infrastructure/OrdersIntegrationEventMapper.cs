using Loja.Orders.Contracts;
using Loja.Orders.Domain;
using Loja.Outbox;
using Loja.SharedKernel;

namespace Loja.Orders.Infrastructure;

/// <summary>
/// Which domain events of the Orders module other modules hear about, and in which public shape.
/// </summary>
internal sealed class OrdersIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        OrderPlaced placed => new OrderPlacedIntegrationEvent(
            placed.OrderId.Value, placed.CustomerId, placed.Total.Amount, placed.Total.Currency),
        OrderCancelled cancelled => new OrderCancelledIntegrationEvent(cancelled.OrderId.Value, cancelled.Reason, cancelled.Expired),
        OrderPaid paid => new OrderPaidIntegrationEvent(paid.OrderId.Value),
        OrderShipped shipped => new OrderShippedIntegrationEvent(shipped.OrderId.Value, shipped.TrackingCode),
        _ => null, // stays inside the module
    };
}
