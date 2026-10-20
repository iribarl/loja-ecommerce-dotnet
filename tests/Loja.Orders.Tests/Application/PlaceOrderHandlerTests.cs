using Loja.Cart.Contracts;
using Loja.Customers.Contracts;
using Loja.Orders.Application.PlaceOrder;
using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Tests.Application;

public class PlaceOrderHandlerTests
{
    private readonly FakeCustomers _customers = new();
    private readonly FakeCart _cart = new();
    private readonly FakeCatalog _catalog = new();
    private readonly FakeStock _stock = new();
    private readonly InMemoryOrders _orders = new();
    private readonly Guid _customer = Guid.NewGuid();

    public PlaceOrderHandlerTests() =>
        _customers.Customers[_customer] = new CustomerInfo(_customer, "Maria da Silva", "maria@exemplo.com");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PlaceOrderHandler Handler =>
        new(_customers, _cart, _catalog, new FakeShipping(), _stock, _orders, TimeProvider.System);

    [Fact]
    public async Task ValidCart_PlacesTheOrder_ReservesStock_AndEmptiesTheCart()
    {
        var shirt = _catalog.Add(price: 49.90m);
        var jacket = _catalog.Add(price: 229.90m);
        PutInCart((shirt.VariantId, 2), (jacket.VariantId, 1));

        var result = await Handler.HandleAsync(Request(), Ct);

        var order = _orders.Orders.ShouldHaveSingleItem();
        order.Id.Value.ShouldBe(result.Value);
        order.Subtotal.ShouldBe(Money.Of(329.70m));
        order.Shipping.Price.ShouldBe(Money.Of(19.90m));
        order.Customer.Email.ShouldBe("maria@exemplo.com");
        _stock.Reserved.ShouldHaveSingleItem().OrderId.ShouldBe(order.Id.Value);
        _cart.Carts.ShouldNotContainKey(_customer);
    }

    [Fact]
    public async Task PricesAreFrozen_ChangingTheCatalogLaterDoesNotChangeTheOrder()
    {
        var shirt = _catalog.Add(price: 49.90m);
        PutInCart((shirt.VariantId, 1));

        await Handler.HandleAsync(Request(), Ct);
        _catalog.Variants[shirt.VariantId] = shirt with { Price = 99.90m };

        _orders.Orders.Single().Lines.Single().UnitPrice.ShouldBe(Money.Of(49.90m));
    }

    [Fact]
    public async Task UnknownCustomer_Fails()
    {
        var result = await Handler.HandleAsync(Request() with { CustomerId = Guid.NewGuid() }, Ct);

        result.Error.ShouldBe(PlaceOrderErrors.CustomerNotFound);
    }

    [Fact]
    public async Task EmptyCart_Fails()
    {
        (await Handler.HandleAsync(Request(), Ct)).Error.ShouldBe(PlaceOrderErrors.EmptyCart);
    }

    [Theory]
    [InlineData(false, 10, 1)] // unpublished
    [InlineData(true, 1, 2)]   // less stock than in the cart
    public async Task CartThatNoLongerFitsTheCatalog_Fails(bool published, int stock, int quantity)
    {
        var shirt = _catalog.Add(price: 49.90m, stock: stock, published: published);
        PutInCart((shirt.VariantId, quantity));

        (await Handler.HandleAsync(Request(), Ct)).Error.ShouldBe(PlaceOrderErrors.CartChanged);
        _stock.Reserved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("123", "SP")]       // invalid CEP
    [InlineData("01310-100", "XX")] // invalid state
    public async Task InvalidAddress_Fails_WithoutTakingAnyStock(string cep, string state)
    {
        var shirt = _catalog.Add(price: 49.90m);
        PutInCart((shirt.VariantId, 1));

        var result = await Handler.HandleAsync(Request() with { Cep = cep, State = state }, Ct);

        result.IsFailure.ShouldBeTrue();
        _stock.Reserved.ShouldBeEmpty();
        _orders.Orders.ShouldBeEmpty();
    }

    [Fact]
    public async Task StockTakenByAnotherCustomer_FailsWithoutAnOrder_AndKeepsTheCart()
    {
        var shirt = _catalog.Add(price: 49.90m);
        PutInCart((shirt.VariantId, 1));
        _stock.Fail = true;

        (await Handler.HandleAsync(Request(), Ct)).Error.ShouldBe(PlaceOrderErrors.OutOfStock);

        _orders.Orders.ShouldBeEmpty();
        _cart.Carts.ShouldContainKey(_customer);
    }

    [Fact]
    public async Task WhenSavingTheOrderFails_TheReservedStockIsReleased()
    {
        var shirt = _catalog.Add(price: 49.90m);
        PutInCart((shirt.VariantId, 1));
        _orders.FailToSave = true;

        await Should.ThrowAsync<InvalidOperationException>(() => Handler.HandleAsync(Request(), Ct));

        _stock.Released.ShouldHaveSingleItem().ShouldBe(_stock.Reserved.Single().OrderId);
    }

    [Fact]
    public async Task WhenEmptyingTheCartFails_TheOrderIsStillPlaced()
    {
        var shirt = _catalog.Add(price: 49.90m);
        PutInCart((shirt.VariantId, 1));
        _cart.FailToClear = true;

        (await Handler.HandleAsync(Request(), Ct)).IsSuccess.ShouldBeTrue();

        _orders.Orders.ShouldHaveSingleItem().Status.ShouldBe(OrderStatus.Created);
    }

    private void PutInCart(params (Guid Variant, int Quantity)[] lines) =>
        _cart.Carts[_customer] = lines.Select(l => new CartLine(l.Variant, l.Quantity)).ToList();

    private PlaceOrderRequest Request() =>
        new(_customer, "Maria da Silva", "30130-000", "Avenida Afonso Pena", "1000", null, "Centro", "Belo Horizonte", "MG");
}
