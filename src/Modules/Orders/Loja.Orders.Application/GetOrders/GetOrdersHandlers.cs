using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Application.GetOrders;

public sealed class GetMyOrdersHandler(IOrderRepository orders)
{
    public async Task<IReadOnlyList<OrderSummaryView>> HandleAsync(Guid customerId, CancellationToken cancellationToken) =>
        (await orders.ListByCustomerAsync(customerId, cancellationToken)).Select(o => o.ToSummary()).ToList();
}

public sealed class GetMyOrderHandler(IOrderRepository orders)
{
    public async Task<Result<OrderView>> HandleAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(new OrderId(orderId), cancellationToken);

        // Another customer's order is "not found", never "forbidden": it must not reveal that it exists.
        return order is not null && order.Customer.CustomerId == customerId
            ? order.ToView()
            : OrderErrors.NotFound;
    }
}
