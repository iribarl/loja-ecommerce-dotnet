using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Customers.Infrastructure;
using Loja.IntegrationTests.Customers;
using Loja.Orders.Application.PlaceOrder;
using Loja.Web.Orders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Payments;

/// <summary>
/// Paying as the shopper does: Pix on the order page, the development "bank app", and the page
/// noticing the payment.
/// </summary>
public sealed partial class PaymentPagesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task AfterCheckout_TheOrderPageShowsThePix()
    {
        var (_, _, orderPage) = await CheckedOut();

        orderPage.Html.ShouldContain("Pagamento via Pix");
        orderPage.Html.ShouldContain("Pix copia e cola");
        orderPage.Html.ShouldContain(">000201"); // the BR Code starts with its format indicator
        orderPage.Html.ShouldContain("Pague até");
        orderPage.Html.ShouldContain("Simular pagamento");
    }

    [Fact]
    public async Task SimulatedPayment_ConfirmsTheOrder()
    {
        var (browser, orderId, orderPage) = await CheckedOut();
        var paymentId = SimulateForm().Match(orderPage.Html).Groups["payment"].Value;

        await browser.PostAsync($"/dev/pix/{paymentId}/pagar", formName: null, orderPage.AntiforgeryToken,
            new() { ["returnUrl"] = $"/pedidos/{orderId}" });

        // What js/order.js does: ask the status until it changes.
        await ShoppingSteps.Eventually(async () => await Status(browser, orderId) == "Paid");
        (await browser.GetAsync($"/pedidos/{orderId}")).Html.ShouldContain("Pagamento confirmado!");
    }

    [Fact]
    public async Task StatusEndpoint_IsOnlyForTheOrderOwner()
    {
        var (_, orderId, _) = await CheckedOut();
        var stranger = await BrowserSession.RegisterAsync(postgres, "Outra Pessoa", BrowserSession.NewEmail(), app: postgres.ShoppingWeb);

        var response = await stranger.Client.GetAsync($"/pedidos/{orderId}/status", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrderWithoutPix_OffersToGenerateIt()
    {
        var email = BrowserSession.NewEmail();
        var browser = await BrowserSession.RegisterAsync(postgres, "Cliente Pix", email, app: postgres.ShoppingWeb);
        var orderId = await PlaceOrderWithoutPix(email);

        var page = await browser.GetAsync($"/pedidos/{orderId}");
        page.Html.ShouldContain("Gere o Pix para pagar este pedido.");

        var withPix = await browser.PostAsync($"/pedidos/{orderId}", "generate-pix", page.AntiforgeryToken, []);
        withPix.Html.ShouldContain("Pix copia e cola");
    }

    private async Task<(BrowserSession Browser, Guid OrderId, BrowserSession.Page OrderPage)> CheckedOut()
    {
        var email = BrowserSession.NewEmail();
        var browser = await BrowserSession.RegisterAsync(postgres, "Cliente Pix", email, app: postgres.ShoppingWeb);
        await AddToCart(email);

        var orderPage = await browser.SubmitAsync("/checkout", "checkout", new()
        {
            ["Input.Action"] = "place",
            ["Input.Recipient"] = "Cliente Pix",
            ["Input.Cep"] = "01310-100",
            ["Input.Street"] = "Avenida Paulista",
            ["Input.Number"] = "1000",
            ["Input.District"] = "Bela Vista",
            ["Input.City"] = "São Paulo",
            ["Input.State"] = "SP",
        });

        var orderId = Guid.Parse(orderPage.Url.AbsolutePath.Split('/')[^1]);
        return (browser, orderId, orderPage);
    }

    private static async Task<string> Status(BrowserSession browser, Guid orderId) =>
        (await browser.Client.GetFromJsonAsync<OrderStatusResponse>($"/pedidos/{orderId}/status", Ct))!.Status;

    private async Task AddToCart(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var customer = await scope.ServiceProvider.GetRequiredService<UserManager<Customer>>().FindByEmailAsync(email);
        var products = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.AsNoTracking()
            .Include(p => p.Variants).Where(p => p.IsPublished).ToListAsync(Ct);
        var variant = products.SelectMany(p => p.Variants).Where(v => v.Stock >= 3).OrderBy(_ => Guid.NewGuid()).First();

        (await scope.ServiceProvider.GetRequiredService<AddToCartHandler>()
            .HandleAsync(new CartOwner(Guid.NewGuid(), customer!.Id), variant.Id.Value, 1, Ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task<Guid> PlaceOrderWithoutPix(string email)
    {
        await AddToCart(email);
        await using var scope = Services.CreateAsyncScope();
        var customer = await scope.ServiceProvider.GetRequiredService<UserManager<Customer>>().FindByEmailAsync(email);
        var order = await scope.ServiceProvider.GetRequiredService<PlaceOrderHandler>().HandleAsync(
            new PlaceOrderRequest(customer!.Id, "Cliente Pix", "01310-100", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP"), Ct);
        return order.Value;
    }

    [GeneratedRegex("action=\"/dev/pix/(?<payment>[0-9a-f-]{36})/pagar\"")]
    private static partial Regex SimulateForm();
}
