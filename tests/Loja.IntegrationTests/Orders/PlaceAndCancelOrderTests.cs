using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Cart.Contracts;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Customers.Infrastructure;
using Loja.Orders.Application.CancelOrder;
using Loja.Orders.Application.PlaceOrder;
using Loja.Orders.Domain;
using Loja.Orders.Infrastructure.Persistence;
using Loja.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Orders;

/// <summary>
/// End to end, with the real app (real DI, real databases, outbox processor running in the background):
/// buying takes the stock, cancelling gives it back through the outbox.
/// </summary>
public sealed class PlaceAndCancelOrderTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task PlaceThenCancel_StockGoesOutAndComesBackThroughTheOutbox()
    {
        var customer = await NewCustomer();
        var (variant, stockBefore) = await AVariantWithStock(minimum: 3);
        await AddToCart(customer, variant, quantity: 2);

        // Place the order.
        var orderId = await InScope(sp => sp.GetRequiredService<PlaceOrderHandler>()
            .HandleAsync(Request(customer), Ct));
        orderId.IsSuccess.ShouldBeTrue(orderId.IsFailure ? orderId.Error.Message : null);

        (await StockOf(variant)).ShouldBe(stockBefore - 2);
        (await InScope(sp => sp.GetRequiredService<ICartApi>().GetCustomerCartAsync(customer, Ct))).ShouldBeEmpty();
        (await OutboxTypes()).ShouldContain(t => t.Contains("OrderPlacedIntegrationEvent", StringComparison.Ordinal));

        // Cancel it.
        var cancelled = await InScope(sp => sp.GetRequiredService<CancelOrderHandler>()
            .HandleAsync(customer, orderId.Value.Value, Ct));
        cancelled.IsSuccess.ShouldBeTrue();

        // The app's outbox processor delivers OrderCancelled to the Catalog in the background.
        await Eventually(async () => await StockOf(variant) == stockBefore);

        await using var scope = Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await catalog.StockReservations.Where(r => r.OrderId == orderId.Value.Value).Select(r => r.Status).ToListAsync(Ct))
            .ShouldAllBe(status => status == ReservationStatus.Released);
        (await catalog.Set<InboxMessage>().AnyAsync(m => m.Consumer.Contains("release-stock"), Ct)).ShouldBeTrue();

        var orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var order = await orders.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId.Value, Ct);
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.History.Select(h => h.Status).ShouldBe([OrderStatus.Created, OrderStatus.Cancelled]);
    }

    [Fact]
    public async Task OrderKeepsItsLinesAddressAndShipping()
    {
        var customer = await NewCustomer();
        var (variant, _) = await AVariantWithStock(minimum: 1);
        await AddToCart(customer, variant, quantity: 1);

        var orderId = (await InScope(sp => sp.GetRequiredService<PlaceOrderHandler>().HandleAsync(Request(customer), Ct))).Value;

        await using var scope = Services.CreateAsyncScope();
        var order = await scope.ServiceProvider.GetRequiredService<OrdersDbContext>()
            .Orders.AsNoTracking().SingleAsync(o => o.Id == orderId, Ct);
        order.Lines.ShouldHaveSingleItem().Quantity.ShouldBe(1);
        order.ShippingAddress.Cep.ToString().ShouldBe("30130-000");
        order.ShippingAddress.State.ShouldBe("MG");
        order.Shipping.Zone.ShouldBe("Sudeste e Sul");
        order.Customer.CustomerId.ShouldBe(customer);
        order.Total.ShouldBe(order.Subtotal + order.Shipping.Price);
    }

    private static PlaceOrderRequest Request(Guid customer) =>
        new(customer, "Cliente Teste", "30130-000", "Avenida Afonso Pena", "1000", "Sala 5", "Centro", "Belo Horizonte", "MG");

    private async Task<Guid> NewCustomer()
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Customer>>();
        var customer = new Customer
        {
            UserName = $"e2e-{Guid.NewGuid():N}@exemplo.com",
            Email = $"e2e-{Guid.NewGuid():N}@exemplo.com",
            FullName = "Cliente Teste",
            CreatedAt = DateTimeOffset.UtcNow,
            PrivacyPolicyAcceptedAt = DateTimeOffset.UtcNow,
        };
        (await users.CreateAsync(customer, "senha-segura-1")).Succeeded.ShouldBeTrue();
        return customer.Id;
    }

    private async Task<(Guid Variant, int Stock)> AVariantWithStock(int minimum)
    {
        await using var scope = Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var products = await catalog.Products.AsNoTracking().Include(p => p.Variants)
            .Where(p => p.IsPublished).ToListAsync(Ct);

        // A different variant each time, so the tests do not compete for the same units.
        var variant = products.SelectMany(p => p.Variants)
            .Where(v => v.Stock >= minimum)
            .OrderBy(_ => Guid.NewGuid())
            .First();
        return (variant.Id.Value, variant.Stock);
    }

    private async Task AddToCart(Guid customer, Guid variant, int quantity)
    {
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<AddToCartHandler>()
            .HandleAsync(new CartOwner(Guid.NewGuid(), customer), variant, quantity, Ct);
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<int> StockOf(Guid variant)
    {
        await using var scope = Services.CreateAsyncScope();
        var id = new ProductVariantId(variant);
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Set<ProductVariant>().Where(v => v.Id == id).Select(v => v.Stock).SingleAsync(Ct);
    }

    private async Task<List<string>> OutboxTypes()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrdersDbContext>()
            .Set<OutboxMessage>().Select(m => m.Type).ToListAsync(Ct);
    }

    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    // The outbox works in the background: wait a little for the effect instead of a fixed sleep.
    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The expected effect did not happen in time.");
            }

            await Task.Delay(200, Ct);
        }
    }
}
