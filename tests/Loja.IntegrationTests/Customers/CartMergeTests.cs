using System.Net;
using Loja.Cart.Domain;
using Loja.Cart.Infrastructure.Persistence;
using Loja.Web.Cart;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Customers;

/// <summary>
/// The cart a visitor builds before signing in is not lost, and does not leak after signing out.
/// </summary>
public sealed class CartMergeTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_BringsTheAnonymousItemsIntoTheCustomerCart()
    {
        var email = BrowserSession.NewEmail();
        await BrowserSession.RegisterAsync(postgres, "Rita Gomes", email);

        var browser = new BrowserSession(postgres.Web.CreateClient());
        await PutInAnonymousCart(browser, ("CAM-BAS-BRANCO-G", 2));

        await browser.SubmitAsync("/conta/entrar", "login", new()
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "senha-segura-1",
        });

        var cart = await browser.GetAsync("/carrinho");
        cart.Html.ShouldContain("Camiseta Básica Algodão");
        cart.Html.ShouldContain("2 itens no carrinho");
    }

    [Fact]
    public async Task Register_KeepsTheItemsChosenBeforeCreatingTheAccount()
    {
        var browser = new BrowserSession(postgres.Web.CreateClient());
        await PutInAnonymousCart(browser, ("SAI-PLI-VERDE-P", 1));

        await browser.SubmitAsync("/conta/cadastro", "register", new()
        {
            ["Input.FullName"] = "Sofia Nunes",
            ["Input.Email"] = BrowserSession.NewEmail(),
            ["Input.Password"] = "senha-segura-1",
            ["Input.ConfirmPassword"] = "senha-segura-1",
            ["Input.AcceptPrivacyPolicy"] = "true",
        });

        (await browser.GetAsync("/carrinho")).Html.ShouldContain("Saia Plissada");
    }

    [Fact]
    public async Task AfterLogout_TheNextVisitorDoesNotSeeTheCustomerCart()
    {
        var browser = new BrowserSession(postgres.Web.CreateClient());
        await PutInAnonymousCart(browser, ("VES-TUB-PRETO-M", 1));
        await browser.SubmitAsync("/conta/cadastro", "register", new()
        {
            ["Input.FullName"] = "Paula Reis",
            ["Input.Email"] = BrowserSession.NewEmail(),
            ["Input.Password"] = "senha-segura-1",
            ["Input.ConfirmPassword"] = "senha-segura-1",
            ["Input.AcceptPrivacyPolicy"] = "true",
        });

        var home = await browser.GetAsync("/");
        await browser.PostAsync("/conta/sair", formName: null, home.AntiforgeryToken, []);

        var cart = await browser.GetAsync("/carrinho");
        cart.Html.ShouldContain("Seu carrinho está vazio.");
    }

    // Visits the store (receiving the cart cookie) and puts items in that anonymous cart.
    private async Task PutInAnonymousCart(BrowserSession browser, params (string Sku, int Quantity)[] lines)
    {
        var response = await browser.Client.GetAsync("/", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CartCookie.Name + "=", StringComparison.Ordinal));
        var cartId = Guid.ParseExact(cookie[(CartCookie.Name.Length + 1)..cookie.IndexOf(';', StringComparison.Ordinal)], "N");

        await using var catalog = postgres.CreateCatalogDbContext();
        var skus = lines.Select(l => l.Sku).ToArray();
        var variants = (await catalog.Products.AsNoTracking().Include(p => p.Variants).ToListAsync(Ct))
            .SelectMany(p => p.Variants)
            .Where(v => skus.Contains(v.Sku.Value))
            .ToDictionary(v => v.Sku.Value, v => v.Id.Value);

        var cart = ShoppingCart.CreateAnonymous(new CartId(cartId), Now);
        foreach (var (sku, quantity) in lines)
        {
            cart.AddItem(variants[sku], quantity, Now);
        }

        await using var db = postgres.CreateCartDbContext();
        db.Carts.Add(cart);
        await db.SaveChangesAsync(Ct);
    }
}
