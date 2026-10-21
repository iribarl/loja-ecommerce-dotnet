using Loja.Orders.Application;
using Loja.Orders.Contracts;
using Loja.Orders.Domain;

namespace Loja.Orders.Infrastructure;

internal sealed class OrdersApi(IOrderRepository orders) : IOrdersApi
{
    public async Task<OrderForPayment?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(new OrderId(orderId), cancellationToken);

        return order is null
            ? null
            : new OrderForPayment(
                order.Id.Value,
                order.Customer.CustomerId,
                order.Customer.Email,
                order.Id.ShortCode,
                order.Total.Amount,
                order.Total.Currency,
                CanBePaid: order.Status is OrderStatus.Created or OrderStatus.AwaitingPayment);
    }
}
