using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Cart.Application.GetCart;
using Loja.Cart.Application.UpdateCartItem;
using Loja.Cart.Domain;

namespace Loja.Cart.Tests.Application;

public class CartHandlersTests
{
    private readonly FakeCatalogApi _catalog = new();
    private readonly InMemoryCartRepository _carts = new();
    private readonly CartOwner _visitor = new(Guid.NewGuid());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AddToCartHandler Add => new(_carts, _catalog, TimeProvider.System);

    private UpdateCartItemHandler Update => new(_carts, _catalog, TimeProvider.System);

    private GetCartHandler Get => new(_carts, _catalog);

    [Fact]
    public async Task AddToCart_FirstTime_CreatesTheAnonymousCartWithTheCookieId()
    {
        var shirt = _catalog.Add();

        var result = await Add.HandleAsync(_visitor, shirt.VariantId, 2, Ct);

        result.Value.ShouldBe(2);
        var cart = _carts.Carts.ShouldHaveSingleItem();
        cart.Id.ShouldBe(new CartId(_visitor.AnonymousCartId));
        cart.CustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task AddToCart_ForCustomer_UsesTheirOwnCart()
    {
        var shirt = _catalog.Add();
        var customer = new CartOwner(Guid.NewGuid(), CustomerId: Guid.NewGuid());

        await Add.HandleAsync(customer, shirt.VariantId, 1, Ct);

        _carts.Carts.ShouldHaveSingleItem().CustomerId.ShouldBe(customer.CustomerId);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 0)]
    public async Task AddToCart_UnpublishedOrSoldOut_Fails(bool published, int stock)
    {
        var shirt = _catalog.Add(stock: stock, published: published);

        (await Add.HandleAsync(_visitor, shirt.VariantId, 1, Ct)).Error.ShouldBe(CartErrors.ProductUnavailable);
        _carts.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task AddToCart_UnknownVariant_Fails()
    {
        (await Add.HandleAsync(_visitor, Guid.NewGuid(), 1, Ct)).Error.ShouldBe(CartErrors.ProductUnavailable);
    }

    [Fact]
    public async Task AddToCart_CountsUnitsAlreadyInTheCartAgainstStock()
    {
        var shirt = _catalog.Add(stock: 3);
        await Add.HandleAsync(_visitor, shirt.VariantId, 2, Ct);

        (await Add.HandleAsync(_visitor, shirt.VariantId, 2, Ct)).Error.ShouldBe(CartErrors.NotEnoughStock);
    }

    [Fact]
    public async Task UpdateItem_ToZero_RemovesEvenIfTheProductNoLongerExists()
    {
        var shirt = _catalog.Add();
        await Add.HandleAsync(_visitor, shirt.VariantId, 1, Ct);
        _catalog.Remove(shirt.VariantId);

        (await Update.HandleAsync(_visitor, shirt.VariantId, 0, Ct)).IsSuccess.ShouldBeTrue();

        _carts.Carts.Single().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateItem_AboveStock_Fails()
    {
        var shirt = _catalog.Add(stock: 2);
        await Add.HandleAsync(_visitor, shirt.VariantId, 1, Ct);

        (await Update.HandleAsync(_visitor, shirt.VariantId, 3, Ct)).Error.ShouldBe(CartErrors.NotEnoughStock);
    }

    [Fact]
    public async Task GetCart_WithoutCart_IsEmpty()
    {
        (await Get.HandleAsync(_visitor, Ct)).ShouldBe(CartView.Empty);
    }

    [Fact]
    public async Task GetCart_UsesCurrentPrices_AndSumsTheSubtotal()
    {
        var shirt = _catalog.Add(price: 49.90m);
        var jacket = _catalog.Add(price: 229.90m);
        await Add.HandleAsync(_visitor, shirt.VariantId, 2, Ct);
        await Add.HandleAsync(_visitor, jacket.VariantId, 1, Ct);

        var view = await Get.HandleAsync(_visitor, Ct);

        view.Lines.Select(l => l.LineTotal).ShouldBe([99.80m, 229.90m]);
        view.Subtotal.ShouldBe(329.70m);
        view.TotalUnits.ShouldBe(3);
        view.CanCheckout.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCart_WhenStockDroppedBelowTheQuantity_FlagsTheLine()
    {
        var shirt = _catalog.Add(stock: 5);
        await Add.HandleAsync(_visitor, shirt.VariantId, 4, Ct);
        _catalog.SetStock(shirt.VariantId, 2);

        var view = await Get.HandleAsync(_visitor, Ct);

        var line = view.Lines.ShouldHaveSingleItem();
        line.Issue.ShouldBe(CartLineIssue.NotEnoughStock);
        line.MaxQuantity.ShouldBe(2);
        view.CanCheckout.ShouldBeFalse();
        view.Subtotal.ShouldBe(0m);
    }

    [Fact]
    public async Task GetCart_WhenProductWasRemoved_KeepsAnUnavailableLine()
    {
        var shirt = _catalog.Add();
        await Add.HandleAsync(_visitor, shirt.VariantId, 1, Ct);
        _catalog.Remove(shirt.VariantId);

        var line = (await Get.HandleAsync(_visitor, Ct)).Lines.ShouldHaveSingleItem();

        line.Issue.ShouldBe(CartLineIssue.Unavailable);
        line.ProductName.ShouldBe("Produto indisponível");
    }
}
