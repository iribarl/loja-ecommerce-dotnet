using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Orders.Domain;
using Loja.Payments.Application.CreatePixCharge;
using Loja.Payments.Application.ExpirePixCharges;
using Loja.Payments.Domain;
using Loja.Payments.Infrastructure.Gateways;
using Loja.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Payments;

/// <summary>
/// Unpaid Pix charges expire, cancelling the order and returning the stock. Instead of waiting
/// 30 minutes, each test moves its own payment's deadline into the past: moving the clock would
/// also expire the pending payments of other tests running at the same time.
/// </summary>
public sealed class PixExpiryTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task UnpaidPix_Expires_CancelsTheOrder_AndReturnsTheStock()
    {
        var (order, paymentId) = await OrderWithPix();
        await MoveDeadlineToThePast(paymentId);

        await RunExpiry();

        (await StatusOf(paymentId)).ShouldBe(PaymentStatus.Expired);
        // PaymentExpired → Orders: Expire → OrderCancelled → Catalog releases the reservation.
        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.Cancelled);
        await ShoppingSteps.Eventually(async () => (await Reservations(order)).All(s => s == ReservationStatus.Released));
    }

    [Fact]
    public async Task PaidAtTheLastSecond_WithTheWebhookStillOnItsWay_IsConfirmedNotExpired()
    {
        var (order, paymentId) = await OrderWithPix();
        Services.GetRequiredService<FakePixGateway>().MarkAsPaid(await ExternalIdOf(paymentId));
        await MoveDeadlineToThePast(paymentId);

        await RunExpiry();

        (await StatusOf(paymentId)).ShouldBe(PaymentStatus.Paid);
        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.Paid);
    }

    private async Task<(Guid Order, PaymentId Payment)> OrderWithPix()
    {
        var (customer, order) = await ShoppingSteps.PlacedOrder(Services);
        await using var scope = Services.CreateAsyncScope();
        var pix = await scope.ServiceProvider.GetRequiredService<CreatePixChargeHandler>().HandleAsync(customer, order, Ct);
        return (order, new PaymentId(pix.Value.PaymentId));
    }

    private async Task MoveDeadlineToThePast(PaymentId payment)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Payments
            .Where(p => p.Id == payment)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
    }

    // The same handler the background sweeper runs every 30 seconds.
    private async Task RunExpiry()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ExpirePixChargesHandler>().HandleAsync(Ct);
    }

    private async Task<PaymentStatus> StatusOf(PaymentId payment)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.Where(p => p.Id == payment).Select(p => p.Status).SingleAsync(Ct);
    }

    private async Task<string> ExternalIdOf(PaymentId payment)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.Where(p => p.Id == payment).Select(p => p.ExternalId).SingleAsync(Ct);
    }

    private async Task<List<ReservationStatus>> Reservations(Guid order)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .StockReservations.Where(r => r.OrderId == order).Select(r => r.Status).ToListAsync(Ct);
    }
}
