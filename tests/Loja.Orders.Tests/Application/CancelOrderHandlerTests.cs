using Loja.Orders.Application.CancelOrder;
using Loja.Orders.Application.GetOrders;
using Loja.Orders.Domain;
using Loja.Orders.Tests.Domain;

namespace Loja.Orders.Tests.Application;

public class CancelOrderHandlerTests
{
    private readonly InMemoryOrders _orders = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CancelOrderHandler Cancel => new(_orders, TimeProvider.System);

    [Fact]
    public async Task OwnUnpaidOrder_IsCancelled_AndRaisesTheEvent()
    {
        var order = Saved(OrderStatus.AwaitingPayment);

        (await Cancel.HandleAsync(order.Customer.CustomerId, order.Id.Value, Ct)).IsSuccess.ShouldBeTrue();

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderCancelled>();
    }

    [Fact]
    public async Task AnotherCustomersOrder_IsNotFound()
    {
        var order = Saved(OrderStatus.Created);

        (await Cancel.HandleAsync(Guid.NewGuid(), order.Id.Value, Ct)).Error.ShouldBe(OrderErrors.NotFound);
        order.Status.ShouldBe(OrderStatus.Created);
    }

    [Fact]
    public async Task PaidOrder_CannotBeCancelledByTheCustomer()
    {
        var order = Saved(OrderStatus.Paid);

        (await Cancel.HandleAsync(order.Customer.CustomerId, order.Id.Value, Ct)).Error.Code.ShouldBe("Orders.InvalidTransition");
    }

    [Fact]
    public async Task GetMyOrder_ForAnotherCustomer_IsNotFound()
    {
        var order = Saved(OrderStatus.Created);

        var result = await new GetMyOrderHandler(_orders).HandleAsync(Guid.NewGuid(), order.Id.Value, Ct);

        result.Error.ShouldBe(OrderErrors.NotFound);
    }

    private Order Saved(OrderStatus status)
    {
        var order = OrderBuilder.InStatus(status);
        _orders.Orders.Add(order);
        return order;
    }
}
