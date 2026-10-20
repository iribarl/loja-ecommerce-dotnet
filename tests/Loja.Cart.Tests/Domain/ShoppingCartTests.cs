using Loja.Cart.Domain;

namespace Loja.Cart.Tests.Domain;

public class ShoppingCartTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Shirt = Guid.NewGuid();
    private static readonly Guid Jacket = Guid.NewGuid();

    [Fact]
    public void AddItem_SameVariantTwice_SumsQuantities()
    {
        var cart = NewCart();

        cart.AddItem(Shirt, 2, Now);
        cart.AddItem(Shirt, 3, Now);

        cart.Items.ShouldHaveSingleItem().Quantity.ShouldBe(5);
        cart.TotalUnits.ShouldBe(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_NonPositiveQuantity_Fails(int quantity)
    {
        NewCart().AddItem(Shirt, quantity, Now).Error.ShouldBe(CartErrors.InvalidQuantity);
    }

    [Fact]
    public void AddItem_OverTheLimitPerItem_Fails_AndKeepsTheCartUnchanged()
    {
        var cart = NewCart();
        cart.AddItem(Shirt, 8, Now);

        cart.AddItem(Shirt, 3, Now).Error.ShouldBe(CartErrors.TooManyUnits);

        cart.QuantityOf(Shirt).ShouldBe(8);
    }

    [Fact]
    public void AddItem_UpdatesLastChange()
    {
        var cart = NewCart();
        var later = Now.AddHours(1);

        cart.AddItem(Shirt, 1, later);

        cart.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void SetQuantity_Zero_RemovesTheLine()
    {
        var cart = NewCart();
        cart.AddItem(Shirt, 2, Now);

        cart.SetQuantity(Shirt, 0, Now).IsSuccess.ShouldBeTrue();

        cart.Items.ShouldBeEmpty();
    }

    [Fact]
    public void SetQuantity_UnknownVariant_Fails()
    {
        NewCart().SetQuantity(Shirt, 1, Now).Error.ShouldBe(CartErrors.ItemNotFound);
    }

    [Fact]
    public void AssignTo_AnonymousCart_BecomesTheCustomers()
    {
        var cart = NewCart();
        var customer = Guid.NewGuid();

        cart.AssignTo(customer, Now).IsSuccess.ShouldBeTrue();

        cart.CustomerId.ShouldBe(customer);
    }

    [Fact]
    public void AssignTo_CustomerCart_Fails()
    {
        var cart = ShoppingCart.CreateForCustomer(Guid.NewGuid(), Now);

        cart.AssignTo(Guid.NewGuid(), Now).Error.ShouldBe(CartErrors.AlreadyOwned);
    }

    [Fact]
    public void Merge_SumsCommonLines_AddsNewOnes_AndCapsAtTheLimit()
    {
        var customerCart = ShoppingCart.CreateForCustomer(Guid.NewGuid(), Now);
        customerCart.AddItem(Shirt, 7, Now);

        var anonymousCart = NewCart();
        anonymousCart.AddItem(Shirt, 5, Now);
        anonymousCart.AddItem(Jacket, 1, Now);

        customerCart.Merge(anonymousCart, Now);

        customerCart.QuantityOf(Shirt).ShouldBe(ShoppingCart.MaxUnitsPerItem);
        customerCart.QuantityOf(Jacket).ShouldBe(1);
    }

    private static ShoppingCart NewCart() => ShoppingCart.CreateAnonymous(CartId.New(), Now);
}
