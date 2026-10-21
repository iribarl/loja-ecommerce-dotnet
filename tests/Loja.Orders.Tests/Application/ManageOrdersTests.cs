using Loja.Orders.Application.ManageOrders;
using Loja.Orders.Domain;
using Loja.Orders.Tests.Domain;

namespace Loja.Orders.Tests.Application;

public sealed class ManageOrdersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryOrders _orders = new();

    private ChangeOrderStatusHandler Change => new(_orders, TimeProvider.System);

    [Theory]
    [InlineData(OrderStatus.Created, new[] { OrderAction.Cancel })]
    [InlineData(OrderStatus.AwaitingPayment, new[] { OrderAction.Cancel })]
    [InlineData(OrderStatus.Paid, new[] { OrderAction.StartPicking })]
    [InlineData(OrderStatus.Picking, new[] { OrderAction.Ship })]
    [InlineData(OrderStatus.Shipped, new[] { OrderAction.Deliver })]
    [InlineData(OrderStatus.Delivered, new OrderAction[0])]
    [InlineData(OrderStatus.Cancelled, new OrderAction[0])]
    public async Task AdminView_OffersTheActionsOfItsStatus(OrderStatus status, OrderAction[] actions)
    {
        var order = Stored(status);

        var view = await new GetAdminOrderHandler(_orders).HandleAsync(order.Id.Value, Ct);

        view.Value.Actions.ShouldBe(actions);
        view.Value.CustomerEmail.ShouldBe("maria@exemplo.com");
    }

    [Fact]
    public async Task WalksAPaidOrderToDelivered()
    {
        var order = Stored(OrderStatus.Paid);

        (await Change.HandleAsync(order.Id.Value, OrderAction.StartPicking, null, Ct)).IsSuccess.ShouldBeTrue();
        (await Change.HandleAsync(order.Id.Value, OrderAction.Ship, "BR123456789BR", Ct)).IsSuccess.ShouldBeTrue();
        (await Change.HandleAsync(order.Id.Value, OrderAction.Deliver, null, Ct)).IsSuccess.ShouldBeTrue();

        order.Status.ShouldBe(OrderStatus.Delivered);
        order.TrackingCode.ShouldBe("BR123456789BR");
    }

    [Fact]
    public async Task ActionNotAllowedNow_Fails_AndSavesNothing()
    {
        var order = Stored(OrderStatus.AwaitingPayment);

        var result = await Change.HandleAsync(order.Id.Value, OrderAction.Ship, "BR1", Ct);

        result.Error.Code.ShouldBe("Orders.InvalidTransition");
        _orders.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task Cancel_ByTheStore_NeedsAReason_AndRecordsIt()
    {
        var order = Stored(OrderStatus.AwaitingPayment);

        (await Change.HandleAsync(order.Id.Value, OrderAction.Cancel, "  ", Ct)).Error.ShouldBe(OrderErrors.CancellationReasonRequired);
        (await Change.HandleAsync(order.Id.Value, OrderAction.Cancel, "Sem estoque físico", Ct)).IsSuccess.ShouldBeTrue();

        order.CancellationReason.ShouldBe("Cancelado pela loja: Sem estoque físico");
        order.DomainEvents.OfType<OrderCancelled>().ShouldHaveSingleItem(); // the stock goes back
    }

    [Fact]
    public async Task OrderChangedMeanwhile_IsReported()
    {
        var order = Stored(OrderStatus.Paid);
        _orders.ChangedByOthers = true;

        var result = await Change.HandleAsync(order.Id.Value, OrderAction.StartPicking, null, Ct);

        result.Error.ShouldBe(OrderErrors.ChangedMeanwhile);
    }

    [Fact]
    public async Task List_FiltersByStatusAndCode_AndCountsWhatIsToPick()
    {
        var paid = Stored(OrderStatus.Paid);
        Stored(OrderStatus.Paid);
        Stored(OrderStatus.Shipped);
        var handler = new ListAdminOrdersHandler(_orders);

        (await handler.HandleAsync("Paid", null, 1, Ct)).TotalCount.ShouldBe(2);
        (await handler.HandleAsync("Bogus", null, 1, Ct)).TotalCount.ShouldBe(3);
        (await handler.HandleAsync(null, paid.Id.ShortCode[^5..].ToLowerInvariant(), 1, Ct)).Items.ShouldContain(o => o.Id == paid.Id.Value);
        (await handler.CountToPickAsync(Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task UnknownOrder_IsNotFound()
    {
        (await Change.HandleAsync(Guid.NewGuid(), OrderAction.Deliver, null, Ct)).Error.ShouldBe(OrderErrors.NotFound);
        (await new GetAdminOrderHandler(_orders).HandleAsync(Guid.NewGuid(), Ct)).Error.ShouldBe(OrderErrors.NotFound);
    }

    private Order Stored(OrderStatus status)
    {
        var order = OrderBuilder.InStatus(status);
        _orders.Orders.Add(order);
        return order;
    }
}
