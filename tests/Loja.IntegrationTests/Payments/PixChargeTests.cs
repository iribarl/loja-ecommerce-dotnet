using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Customers.Infrastructure;
using Loja.Orders.Application.PlaceOrder;
using Loja.Orders.Domain;
using Loja.Orders.Infrastructure.Persistence;
using Loja.Payments.Application.CreatePixCharge;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Payments;

/// <summary>
/// Shared steps for the payment tests on the shopping app: a customer with a placed order.
/// </summary>
internal static class ShoppingSteps
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<(Guid Customer, Guid Order)> PlacedOrder(IServiceProvider services, int quantity = 1)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var customer = new Customer
        {
            UserName = $"pix-{Guid.NewGuid():N}@exemplo.com",
            Email = $"pix-{Guid.NewGuid():N}@exemplo.com",
            FullName = "Cliente Pix",
            CreatedAt = DateTimeOffset.UtcNow,
            PrivacyPolicyAcceptedAt = DateTimeOffset.UtcNow,
        };
        (await sp.GetRequiredService<UserManager<Customer>>().CreateAsync(customer, "senha-segura-1")).Succeeded.ShouldBeTrue();

        var products = await sp.GetRequiredService<CatalogDbContext>().Products.AsNoTracking()
            .Include(p => p.Variants).Where(p => p.IsPublished).ToListAsync(Ct);
        var variant = products.SelectMany(p => p.Variants).Where(v => v.Stock >= quantity + 2).OrderBy(_ => Guid.NewGuid()).First();
        (await sp.GetRequiredService<AddToCartHandler>()
            .HandleAsync(new CartOwner(Guid.NewGuid(), customer.Id), variant.Id.Value, quantity, Ct)).IsSuccess.ShouldBeTrue();

        var order = await sp.GetRequiredService<PlaceOrderHandler>().HandleAsync(
            new PlaceOrderRequest(customer.Id, "Cliente Pix", "01310-100", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"), Ct);
        order.IsSuccess.ShouldBeTrue();

        return (customer.Id, order.Value);
    }

    public static async Task<OrderStatus> StatusOf(IServiceProvider services, Guid orderId)
    {
        await using var scope = services.CreateAsyncScope();
        var id = new OrderId(orderId);
        return await scope.ServiceProvider.GetRequiredService<OrdersDbContext>()
            .Orders.Where(o => o.Id == id).Select(o => o.Status).SingleAsync(Ct);
    }

    /// <summary>Background work (outbox) takes a moment: wait for the effect instead of a fixed sleep.</summary>
    public static async Task Eventually(Func<Task<bool>> condition)
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

public sealed class PixChargeTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task CreatingThePix_PutsTheOrderInAwaitingPayment()
    {
        var (customer, order) = await ShoppingSteps.PlacedOrder(Services);

        await using var scope = Services.CreateAsyncScope();
        var pix = await scope.ServiceProvider.GetRequiredService<CreatePixChargeHandler>().HandleAsync(customer, order, Ct);

        pix.IsSuccess.ShouldBeTrue();
        pix.Value.PixCopyPaste.ShouldStartWith("000201");
        pix.Value.PixQrCodeImage.ShouldStartWith("data:image/svg+xml;base64,");

        // PixChargeCreated travels through the Payments outbox to the Orders module.
        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.AwaitingPayment);
    }
}
