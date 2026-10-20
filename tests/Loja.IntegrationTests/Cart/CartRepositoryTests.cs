using Loja.Cart.Domain;
using Loja.Cart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Cart;

public sealed class CartRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cart_WithItems_RoundTrips()
    {
        var cart = ShoppingCart.CreateAnonymous(CartId.New(), Now);
        var shirt = Guid.NewGuid();
        var jacket = Guid.NewGuid();
        cart.AddItem(shirt, 2, Now);
        cart.AddItem(jacket, 1, Now.AddMinutes(1));
        await Save(cart);

        await using var db = postgres.CreateCartDbContext();
        var loaded = await new CartRepository(db).GetByIdAsync(cart.Id, Ct);

        loaded.ShouldNotBeNull();
        loaded.Items.OrderBy(i => i.AddedAt).Select(i => (i.VariantId, i.Quantity)).ShouldBe([(shirt, 2), (jacket, 1)]);
        loaded.CustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task ExistingCart_AddLineAndChangeAnother_IsSaved()
    {
        var shirt = Guid.NewGuid();
        var jacket = Guid.NewGuid();
        var cart = ShoppingCart.CreateAnonymous(CartId.New(), Now);
        cart.AddItem(shirt, 1, Now);
        await Save(cart);

        await using (var db = postgres.CreateCartDbContext())
        {
            var loaded = (await new CartRepository(db).GetByIdAsync(cart.Id, Ct))!;
            loaded.AddItem(jacket, 1, Now);
            loaded.SetQuantity(shirt, 3, Now);
            await db.SaveChangesAsync(Ct);
        }

        await using var check = postgres.CreateCartDbContext();
        var saved = (await new CartRepository(check).GetByIdAsync(cart.Id, Ct))!;
        saved.QuantityOf(shirt).ShouldBe(3);
        saved.QuantityOf(jacket).ShouldBe(1);
    }

    [Fact]
    public async Task Cart_ChangedInTwoPlacesAtOnce_SecondSaveFails()
    {
        var cart = ShoppingCart.CreateAnonymous(CartId.New(), Now);
        cart.AddItem(Guid.NewGuid(), 1, Now);
        await Save(cart);

        // Two "tabs" read the same cart.
        await using var tab1 = postgres.CreateCartDbContext();
        await using var tab2 = postgres.CreateCartDbContext();
        var cart1 = (await new CartRepository(tab1).GetByIdAsync(cart.Id, Ct))!;
        var cart2 = (await new CartRepository(tab2).GetByIdAsync(cart.Id, Ct))!;

        cart1.AddItem(Guid.NewGuid(), 1, Now.AddMinutes(1));
        await tab1.SaveChangesAsync(Ct);

        cart2.AddItem(Guid.NewGuid(), 1, Now.AddMinutes(2));
        (await new CartRepository(tab2).SaveChangesAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Customer_CanHaveOnlyOneCart()
    {
        var customer = Guid.NewGuid();
        await Save(ShoppingCart.CreateForCustomer(customer, Now));

        await Should.ThrowAsync<DbUpdateException>(() => Save(ShoppingCart.CreateForCustomer(customer, Now)));
    }

    [Fact]
    public async Task GetByCustomer_FindsTheirCart()
    {
        var customer = Guid.NewGuid();
        var cart = ShoppingCart.CreateForCustomer(customer, Now);
        await Save(cart);

        await using var db = postgres.CreateCartDbContext();
        (await new CartRepository(db).GetByCustomerAsync(customer, Ct))!.Id.ShouldBe(cart.Id);
    }

    private async Task Save(ShoppingCart cart)
    {
        await using var db = postgres.CreateCartDbContext();
        var repository = new CartRepository(db);
        repository.Add(cart);
        await repository.SaveChangesAsync(Ct);
    }
}
