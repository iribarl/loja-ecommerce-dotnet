using System.Net;
using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Customers.Infrastructure;
using Loja.IntegrationTests.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Orders;

/// <summary>
/// Checkout, order page and order history as a browser uses them, on the app instance that may buy.
/// </summary>
public sealed class CheckoutPagesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    [Fact]
    public async Task Checkout_WhenAnonymous_AsksToSignIn()
    {
        var page = await new BrowserSession(postgres.ShoppingWeb.CreateClient()).GetAsync("/checkout");

        page.Url.AbsolutePath.ShouldBe("/conta/entrar");
        page.Url.Query.ShouldContain("returnUrl=%2Fcheckout");
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_GoesBackToTheCart()
    {
        var (browser, _) = await NewCustomer();

        var page = await browser.GetAsync("/checkout");

        page.Url.AbsolutePath.ShouldBe("/carrinho");
    }

    [Fact]
    public async Task CalculateShipping_ShowsZoneAndPriceForTheCep()
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 1);

        var page = await browser.SubmitAsync("/checkout", "checkout", Address("30130-000", action: "quote"));

        page.Html.ShouldContain("Sudeste e Sul");
        page.Html.ShouldContain("19,90");
    }

    [Fact]
    public async Task SearchCep_FillsTheAddressFromTheCep()
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 1);
        var onlyCep = new Dictionary<string, string>
        {
            ["Input.Action"] = "quote",
            ["Input.Recipient"] = "Cliente Checkout",
            ["Input.Cep"] = "01310100",
        };

        var page = await browser.SubmitAsync("/checkout", "checkout", onlyCep);

        page.Html.ShouldContain("value=\"Avenida Paulista\"");
        page.Html.ShouldContain("value=\"Bela Vista\"");
        page.Html.ShouldContain("value=\"São Paulo\"");
        page.Html.ShouldContain("value=\"SP\"");
        page.Html.ShouldContain("value=\"01310-100\"");
        page.Html.ShouldContain("Endereço encontrado. Agora informe o número.");
        page.Html.ShouldContain("12,90"); // shipping to São Paulo capital
    }

    [Theory]
    [InlineData("99999-999", "CEP não encontrado.")]
    [InlineData(FakeAddressLookup.UnavailableCep, "Não foi possível buscar o endereço agora.")]
    public async Task SearchCep_WhenItCannotFindTheAddress_LetsTheShopperTypeIt(string cep, string message)
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 1);
        var fields = Address(cep, action: "quote");

        var page = await browser.SubmitAsync("/checkout", "checkout", fields);

        page.Html.ShouldContain(message);
        page.Html.ShouldContain("value=\"Avenida Afonso Pena\""); // what the shopper typed stays
    }

    [Fact]
    public async Task ConfirmOrder_ShowsTheOrderPage_AndListsItInMyAccount()
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 2);

        var orderPage = await browser.SubmitAsync("/checkout", "checkout", Address("30130-000", action: "place"));

        orderPage.Url.AbsolutePath.ShouldStartWith("/pedidos/");
        orderPage.Html.ShouldContain("Pedido recebido! Pague o Pix abaixo para confirmar.");
        orderPage.Html.ShouldContain("Avenida Afonso Pena, 1000");
        orderPage.Html.ShouldContain("CEP 30130-000");

        var account = await browser.GetAsync("/conta");
        account.Html.ShouldContain(orderPage.Url.AbsolutePath);

        // The cart was emptied by the order.
        (await browser.GetAsync("/carrinho")).Html.ShouldContain("Seu carrinho está vazio.");
    }

    [Fact]
    public async Task ConfirmOrder_WithIncompleteAddress_ShowsTheProblems()
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 1);
        var fields = Address("30130-000", action: "place");
        fields["Input.Street"] = "";
        fields["Input.State"] = "Minas";

        var page = await browser.SubmitAsync("/checkout", "checkout", fields);

        page.Url.AbsolutePath.ShouldBe("/checkout");
        page.Html.ShouldContain("Informe a rua ou avenida.");
        page.Html.ShouldContain("Use a sigla do estado, por exemplo SP.");
    }

    [Fact]
    public async Task CancelOrder_FromTheOrderPage()
    {
        var (browser, customer) = await NewCustomer();
        await AddToCart(customer, quantity: 1);
        var orderPage = await browser.SubmitAsync("/checkout", "checkout", Address("01310-100", action: "place"));

        var cancelled = await browser.PostAsync(orderPage.Url.AbsolutePath, "cancel-order", orderPage.AntiforgeryToken, []);

        cancelled.Html.ShouldContain("Cancelado");
        cancelled.Html.ShouldContain("Motivo do cancelamento");
        cancelled.Html.ShouldNotContain(">Cancelar pedido<");
    }

    [Fact]
    public async Task OrderPage_OfAnotherCustomer_IsNotFound()
    {
        var (owner, ownerId) = await NewCustomer();
        await AddToCart(ownerId, quantity: 1);
        var orderPage = await owner.SubmitAsync("/checkout", "checkout", Address("01310-100", action: "place"));

        var (stranger, _) = await NewCustomer();
        var page = await stranger.GetAsync(orderPage.Url.AbsolutePath);

        page.Status.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(BrowserSession Browser, Guid CustomerId)> NewCustomer()
    {
        var email = BrowserSession.NewEmail();
        var browser = await BrowserSession.RegisterAsync(postgres, "Cliente Checkout", email, app: postgres.ShoppingWeb);

        await using var scope = Services.CreateAsyncScope();
        var customer = await scope.ServiceProvider.GetRequiredService<UserManager<Customer>>().FindByEmailAsync(email);
        return (browser, customer!.Id);
    }

    // Puts one in-stock variant (a random one) in the customer's cart.
    private async Task AddToCart(Guid customer, int quantity)
    {
        await using var scope = Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var products = await catalog.Products.AsNoTracking().Include(p => p.Variants).Where(p => p.IsPublished).ToListAsync(Ct);
        var variant = products.SelectMany(p => p.Variants).Where(v => v.Stock >= quantity + 2).OrderBy(_ => Guid.NewGuid()).First();

        var result = await scope.ServiceProvider.GetRequiredService<AddToCartHandler>()
            .HandleAsync(new CartOwner(Guid.NewGuid(), customer), variant.Id.Value, quantity, Ct);
        result.IsSuccess.ShouldBeTrue();
    }

    private static Dictionary<string, string> Address(string cep, string action) => new()
    {
        ["Input.Action"] = action,
        ["Input.Recipient"] = "Cliente Checkout",
        ["Input.Cep"] = cep,
        ["Input.Street"] = "Avenida Afonso Pena",
        ["Input.Number"] = "1000",
        ["Input.Complement"] = "",
        ["Input.District"] = "Centro",
        ["Input.City"] = "Belo Horizonte",
        ["Input.State"] = "MG",
    };
}
