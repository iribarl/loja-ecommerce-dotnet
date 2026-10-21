using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.IntegrationTests.Payments;
using Loja.Orders.Domain;
using Loja.Payments.Application.CreatePixCharge;
using Loja.Payments.Infrastructure.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Admin;

/// <summary>
/// Handling orders in the panel, on the app instance that may buy.
/// </summary>
public sealed class AdminOrdersPagesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task PaidOrder_IsPicked_Shipped_AndDelivered()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var order = await PaidOrder();
        var url = $"/admin/pedidos/{order}";

        var page = await admin.GetAsync(url);
        page.Html.ShouldContain("Iniciar separação");

        var picking = await admin.PostAsync(url, "start-picking", page.AntiforgeryToken, []);
        picking.Html.ShouldContain("Pedido atualizado.");
        picking.Html.ShouldContain("Marcar como enviado");

        var shipped = await admin.PostAsync(url, "ship", picking.AntiforgeryToken, new() { ["TrackingCode"] = "BR123456789BR" });
        shipped.Html.ShouldContain("Código de rastreio: <strong>BR123456789BR</strong>");

        var delivered = await admin.PostAsync(url, "deliver", shipped.AntiforgeryToken, []);
        delivered.Html.ShouldContain("Entregue");
        delivered.Html.ShouldNotContain("Próximo passo"); // nothing left to do

        (await ShoppingSteps.StatusOf(Services, order)).ShouldBe(OrderStatus.Delivered);
    }

    [Fact]
    public async Task Ship_WithoutTrackingCode_IsExplained()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var order = await PaidOrder();
        var url = $"/admin/pedidos/{order}";
        var picking = await admin.PostAsync(url, "start-picking", (await admin.GetAsync(url)).AntiforgeryToken, []);

        var page = await admin.PostAsync(url, "ship", picking.AntiforgeryToken, new() { ["TrackingCode"] = " " });

        page.Html.ShouldContain("Informe o código de rastreio");
        (await ShoppingSteps.StatusOf(Services, order)).ShouldBe(OrderStatus.Picking);
    }

    [Fact]
    public async Task CancelUnpaidOrder_GivesTheStockBack()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var (_, order) = await ShoppingSteps.PlacedOrder(Services);
        var url = $"/admin/pedidos/{order}";

        var cancelled = await admin.PostAsync(url, "cancel", (await admin.GetAsync(url)).AntiforgeryToken, new() { ["Reason"] = "Cliente pediu por telefone" });

        cancelled.Html.ShouldContain("Motivo do cancelamento: Cancelado pela loja: Cliente pediu por telefone");
        // OrderCancelled → outbox → Catalog releases the reservation.
        await ShoppingSteps.Eventually(async () => await ReservationOf(order) == ReservationStatus.Released);
    }

    [Fact]
    public async Task List_FindsByCode_AndFiltersByStatus()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        var (_, order) = await ShoppingSteps.PlacedOrder(Services);
        var code = new OrderId(order).ShortCode;

        (await admin.GetAsync($"/admin/pedidos?codigo={code}")).Html.ShouldContain($"/admin/pedidos/{order}");
        (await admin.GetAsync($"/admin/pedidos?codigo={code[^6..].ToLowerInvariant()}")).Html.ShouldContain($"/admin/pedidos/{order}");
        (await admin.GetAsync($"/admin/pedidos?codigo={code}&status=Delivered")).Html.ShouldContain("Nenhum pedido encontrado.");
    }

    [Fact]
    public async Task PanelHome_CountsPaidOrdersToPick()
    {
        var admin = await AdminSession.SignInAsync(postgres, postgres.ShoppingWeb);
        await PaidOrder();

        var home = await admin.GetAsync("/admin");

        home.Html.ShouldContain("aguardando");
        home.Html.ShouldContain("separação");
        home.Html.ShouldContain("/admin/pedidos?status=Paid");
    }

    // Placed, charged with Pix and paid through the fake gateway's signed webhook.
    private async Task<Guid> PaidOrder()
    {
        var (customer, order) = await ShoppingSteps.PlacedOrder(Services);

        await using (var scope = Services.CreateAsyncScope())
        {
            var pix = await scope.ServiceProvider.GetRequiredService<CreatePixChargeHandler>().HandleAsync(customer, order, Ct);
            await scope.ServiceProvider.GetRequiredService<IPixPaymentSimulator>().SimulatePaymentAsync(pix.Value.PaymentId, Ct);
        }

        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.Paid);
        return order;
    }

    private async Task<ReservationStatus> ReservationOf(Guid order)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .StockReservations.Where(r => r.OrderId == order).Select(r => r.Status).SingleAsync(Ct);
    }
}
