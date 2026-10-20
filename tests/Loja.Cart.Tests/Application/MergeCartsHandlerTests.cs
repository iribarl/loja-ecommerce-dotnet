using Loja.Cart.Application;
using Loja.Cart.Application.GetCartUnits;
using Loja.Cart.Application.MergeCarts;
using Loja.Cart.Domain;

namespace Loja.Cart.Tests.Application;

public class MergeCartsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryCartRepository _carts = new();
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _shirt = Guid.NewGuid();
    private readonly Guid _jacket = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private MergeCartsHandler Merge => new(_carts, TimeProvider.System);

    [Fact]
    public async Task CustomerWithoutCart_TakesTheAnonymousCart()
    {
        var anonymous = AnonymousCartWith((_shirt, 2));

        await Merge.HandleAsync(anonymous.Id.Value, _customer, Ct);

        var cart = _carts.Carts.ShouldHaveSingleItem();
        cart.CustomerId.ShouldBe(_customer);
        cart.QuantityOf(_shirt).ShouldBe(2);
    }

    [Fact]
    public async Task CustomerWithCart_ReceivesTheAnonymousItems_AndTheAnonymousCartIsDeleted()
    {
        var customerCart = ShoppingCart.CreateForCustomer(_customer, Now);
        customerCart.AddItem(_shirt, 1, Now);
        _carts.Add(customerCart);
        var anonymous = AnonymousCartWith((_shirt, 2), (_jacket, 1));

        await Merge.HandleAsync(anonymous.Id.Value, _customer, Ct);

        _carts.Carts.ShouldHaveSingleItem().ShouldBeSameAs(customerCart);
        customerCart.QuantityOf(_shirt).ShouldBe(3);
        customerCart.QuantityOf(_jacket).ShouldBe(1);
    }

    [Fact]
    public async Task WithoutAnonymousCart_DoesNothing()
    {
        await Merge.HandleAsync(Guid.NewGuid(), _customer, Ct);

        _carts.Carts.ShouldBeEmpty();
        _carts.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task NeverTakesACartThatBelongsToAnotherCustomer()
    {
        var other = ShoppingCart.CreateForCustomer(Guid.NewGuid(), Now);
        other.AddItem(_shirt, 1, Now);
        _carts.Add(other);

        await Merge.HandleAsync(other.Id.Value, _customer, Ct);

        other.CustomerId.ShouldNotBe(_customer);
        _carts.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task Visitor_NeverSeesACustomersCart_EvenWithItsIdInTheCookie()
    {
        var customerCart = ShoppingCart.CreateForCustomer(_customer, Now);
        customerCart.AddItem(_shirt, 3, Now);
        _carts.Add(customerCart);

        var units = await new GetCartUnitsHandler(_carts).HandleAsync(new CartOwner(customerCart.Id.Value), Ct);

        units.ShouldBe(0);
    }

    private ShoppingCart AnonymousCartWith(params (Guid Variant, int Quantity)[] lines)
    {
        var cart = ShoppingCart.CreateAnonymous(CartId.New(), Now);
        foreach (var (variant, quantity) in lines)
        {
            cart.AddItem(variant, quantity, Now);
        }

        _carts.Add(cart);
        return cart;
    }
}
