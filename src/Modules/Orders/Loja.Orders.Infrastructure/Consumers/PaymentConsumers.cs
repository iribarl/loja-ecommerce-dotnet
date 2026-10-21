using Loja.Orders.Application;
using Loja.Orders.Domain;
using Loja.Orders.Infrastructure.Persistence;
using Loja.Outbox;
using Loja.Payments.Contracts;
using Loja.SharedKernel;

namespace Loja.Orders.Infrastructure.Consumers;

/// <summary>A Pix charge exists: the order now waits for its payment.</summary>
internal sealed class MarkOrderAwaitingPayment(OrdersDbContext db, IOrderRepository orders, TimeProvider clock)
    : IIntegrationEventConsumer<PixChargeCreatedIntegrationEvent>
{
    private const string Name = "orders.mark-awaiting-payment";

    public Task ConsumeAsync(PixChargeCreatedIntegrationEvent message, CancellationToken cancellationToken) =>
        OrderEventConsumer.ApplyOnceAsync(db, orders, message, Name, message.OrderId,
            // A second charge for the same order (the first one failed) finds it already waiting: fine.
            order => order.Status == OrderStatus.AwaitingPayment ? Result.Success() : order.AwaitPayment(clock.GetUtcNow()),
            cancellationToken);
}

/// <summary>The gateway confirmed the payment: the order is paid (and OrderPaid goes out through the outbox).</summary>
internal sealed class MarkOrderPaid(OrdersDbContext db, IOrderRepository orders, TimeProvider clock)
    : IIntegrationEventConsumer<PaymentConfirmedIntegrationEvent>
{
    private const string Name = "orders.mark-paid";

    public Task ConsumeAsync(PaymentConfirmedIntegrationEvent message, CancellationToken cancellationToken) =>
        OrderEventConsumer.ApplyOnceAsync(db, orders, message, Name, message.OrderId,
            order => order.Status switch
            {
                OrderStatus.Paid => Result.Success(),
                // The payment can arrive before PixChargeCreated was applied (separate deliveries):
                // the order goes through AwaitingPayment so its history stays complete.
                OrderStatus.Created => order.AwaitPayment(clock.GetUtcNow()) is { IsSuccess: true }
                    ? order.MarkPaid(clock.GetUtcNow())
                    : Result.Success(),
                _ => order.MarkPaid(clock.GetUtcNow()),
            },
            cancellationToken);
}

internal static class OrderEventConsumer
{
    /// <summary>
    /// Applies a change to an order once per message: the inbox row and the order change are saved
    /// in the same transaction, so a redelivered message is skipped.
    /// </summary>
    /// <remarks>
    /// A transition the order refuses (e.g. it was cancelled meanwhile) is not an error to retry:
    /// the message is recorded as handled and the order stays as it is.
    /// </remarks>
    public static async Task ApplyOnceAsync(
        OrdersDbContext db,
        IOrderRepository orders,
        IntegrationEvent message,
        string consumer,
        Guid orderId,
        Func<Order, Result> change,
        CancellationToken cancellationToken)
    {
        if (await db.HasProcessedAsync(message.Id, consumer, cancellationToken))
        {
            return;
        }

        var order = await orders.GetByIdAsync(new OrderId(orderId), cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} not found for {consumer}.");

        change(order);

        db.MarkProcessed(message.Id, consumer, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
}
