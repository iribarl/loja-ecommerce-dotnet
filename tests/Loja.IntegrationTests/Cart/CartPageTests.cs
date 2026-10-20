using System.Net;
using System.Text.RegularExpressions;
using Loja.Cart.Domain;
using Loja.Cart.Infrastructure.Persistence;
using Loja.Web.Cart;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Cart;

/// <summary>
/// The cart as a browser uses it: cookie, page and form posts.
/// </summary>
public sealed partial class CartPageTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstVisit_GetsACartCookie_AndAnEmptyCart()
    {
        var client = postgres.Web.CreateClient();

        var response = await client.GetAsync("/carrinho", Ct);

        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(CartCookie.Name + "=", StringComparison.Ordinal));
        (await Html(response)).ShouldContain("Seu carrinho está vazio.");
    }

    [Fact]
    public async Task CartWithItems_ShowsLinesSubtotalAndHeaderCount()
    {
        var (client, cartId) = await NewVisitor();
        await PutInCart(cartId, ("CAM-BAS-PRETO-M", 2), ("JAQ-CVE-AMARELO-G", 1));

        var html = await Html(await client.GetAsync("/carrinho", Ct));

        html.ShouldContain("Camiseta Básica Algodão");
        html.ShouldContain("Jaqueta Corta-Vento");
        html.ShouldContain("Subtotal (3 itens)");
        html.ShouldContain("329,70"); // 2 x 49,90 + 229,90
        html.ShouldContain("3 itens no carrinho");
    }

    [Fact]
    public async Task RemovingALine_RedirectsAndShowsTheUpdatedCart()
    {
        var (client, cartId) = await NewVisitor();
        var variants = await PutInCart(cartId, ("REG-DRY-AZUL-M", 1));
        var page = await Html(await client.GetAsync("/carrinho", Ct));

        var response = await client.PostAsync("/carrinho", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "cart",
            ["__RequestVerificationToken"] = AntiforgeryToken(page),
            ["Command"] = $"set:{variants["REG-DRY-AZUL-M"]:N}:0",
        }), Ct);

        // The client followed the redirect (Post-Redirect-Get) back to the cart page.
        response.RequestMessage!.Method.ShouldBe(HttpMethod.Get);
        (await Html(response)).ShouldContain("Seu carrinho está vazio.");
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejected()
    {
        var (client, _) = await NewVisitor();

        var response = await client.PostAsync("/carrinho", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "cart",
            ["Command"] = $"set:{Guid.NewGuid():N}:0",
        }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // A new browser: its first request receives the cart cookie, which the client keeps.
    private async Task<(HttpClient Client, Guid CartId)> NewVisitor()
    {
        var client = postgres.Web.CreateClient();
        var response = await client.GetAsync("/", Ct);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CartCookie.Name + "=", StringComparison.Ordinal));
        var value = cookie[(CartCookie.Name.Length + 1)..cookie.IndexOf(';', StringComparison.Ordinal)];
        return (client, Guid.ParseExact(value, "N"));
    }

    private async Task<Dictionary<string, Guid>> PutInCart(Guid cartId, params (string Sku, int Quantity)[] lines)
    {
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
        await new CartRepository(db).SaveAsNewAsync(cart, Ct);
        return variants;
    }

    private static string AntiforgeryToken(string html) =>
        AntiforgeryInput().Match(html).Groups["token"].Value;

    private static async Task<string> Html(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"(?<token>[^\"]+)\"")]
    private static partial Regex AntiforgeryInput();
}

internal static class CartRepositoryTestExtensions
{
    public static async Task SaveAsNewAsync(this CartRepository repository, ShoppingCart cart, CancellationToken ct)
    {
        repository.Add(cart);
        (await repository.SaveChangesAsync(ct)).ShouldBeTrue();
    }
}
