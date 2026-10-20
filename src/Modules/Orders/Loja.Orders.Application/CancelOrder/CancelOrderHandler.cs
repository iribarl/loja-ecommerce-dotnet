using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Application.CancelOrder;

/// <summary>
/// The customer cancels an unpaid order. The stock goes back through the outbox:
/// the order saves OrderCancelled in the same transaction, and the Catalog consumes it.
/// </summary>
public sealed class CancelOrderHandler(IOrderRepository orders, TimeProvider clock)
{
    public const string CustomerReason = "Cancelado pelo cliente.";

    public async Task<Result> HandleAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(new OrderId(orderId), cancellationToken);

        // Someone else's order answers "not found": confirming it exists would leak information.
        if (order is null || order.Customer.CustomerId != customerId)
        {
            return OrderErrors.NotFound;
        }

        var result = order.Cancel(CustomerReason, clock.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await orders.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
