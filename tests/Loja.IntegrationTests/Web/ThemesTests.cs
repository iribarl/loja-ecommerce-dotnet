using System.Net;
using System.Text.RegularExpressions;
using Loja.IntegrationTests.Customers;
using Loja.Themes;
using Loja.Themes.Classico;
using Loja.Themes.Moderno;
using Loja.Web.Theming;

namespace Loja.IntegrationTests.Web;

/// <summary>
/// The store in each theme: the default one, and a visitor trying another with ?tema=.
/// </summary>
public sealed partial class ThemesTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ByDefault_TheStoreUsesTheDefaultTheme()
    {
        var home = await NewBrowser().GetAsync("/");

        home.Html.ShouldContain("class=\"site-header\"");
        home.Html.ShouldNotContain("mod-header");
        home.Html.ShouldContain("Loja.Themes.Classico/theme.");
        home.Html.ShouldNotContain("Loja.Themes.Moderno/theme.");
    }

    [Fact]
    public async Task ChoosingATheme_ChangesTheStructure_AndIsRemembered()
    {
        var browser = NewBrowser();

        var home = await browser.GetAsync("/?tema=moderno");
        home.Html.ShouldContain("mod-header");
        home.Html.ShouldContain("mod-hero");
        home.Html.ShouldContain("Loja.Themes.Moderno/theme.");

        // The cookie keeps the choice on the next pages.
        var listing = await browser.GetAsync("/produtos");
        listing.Html.ShouldContain("class=\"mod-card\"");
        listing.Html.ShouldNotContain("class=\"product-card\"");

        var product = await browser.GetAsync(FirstProductUrl(listing.Html));
        product.Html.ShouldContain("mod-product-gallery");
        product.Html.ShouldContain("Adicionar à sacola");

        // Back to the store's theme.
        (await browser.GetAsync("/?tema=padrao")).Html.ShouldContain("class=\"site-header\"");
        (await browser.GetAsync("/produtos")).Html.ShouldContain("class=\"product-card\"");
    }

    [Fact]
    public async Task UnknownTheme_IsIgnored()
    {
        var home = await NewBrowser().GetAsync("/?tema=nao-existe");

        home.Html.ShouldContain("class=\"site-header\"");
    }

    [Fact]
    public async Task Switcher_OffersEveryTheme_AndMarksTheCurrentOne()
    {
        var home = await NewBrowser().GetAsync("/?tema=moderno");

        home.Html.ShouldContain("Tema da demo:");
        home.Html.ShouldContain("tema=classico");
        CurrentInSwitcher().Match(home.Html).Groups["name"].Value.ShouldBe("Moderno");
    }

    [Fact]
    public async Task EveryStorePage_WorksInTheSecondTheme()
    {
        var browser = NewBrowser();
        await browser.GetAsync("/?tema=moderno");
        var productUrl = FirstProductUrl((await browser.GetAsync("/produtos")).Html);

        foreach (var url in new[] { "/", "/produtos", productUrl, "/carrinho", "/conta/entrar", "/conta/cadastro", "/privacidade" })
        {
            var page = await browser.GetAsync(url);
            page.Status.ShouldBe(HttpStatusCode.OK, url);
            page.Html.ShouldContain("mod-header", customMessage: url);
        }

        var missing = await browser.GetAsync("/nao-existe");
        missing.Status.ShouldBe(HttpStatusCode.NotFound);
        missing.Html.ShouldContain("mod-header");
    }

    [Fact]
    public void Catalog_FillsMissingPartsFromTheDefaultTheme()
    {
        var onlyLayout = new ThemeDefinition("so-layout", "Só layout", typeof(Themes.Moderno.StoreLayout), []);

        var theme = new ThemeCatalog(ClassicoTheme.Definition, [onlyLayout]).Resolve("so-layout");

        theme.Layout.ShouldBe(typeof(Themes.Moderno.StoreLayout));
        theme.ProductCard.ShouldBe(ClassicoTheme.Definition.ProductCard);
        theme.ProductView.ShouldBe(ClassicoTheme.Definition.ProductView);
    }

    [Fact]
    public void Catalog_RejectsMistakesAtStartUp()
    {
        Should.Throw<InvalidOperationException>(() =>
            new ThemeCatalog(ClassicoTheme.Definition, [ClassicoTheme.Definition with { DisplayName = "Outro" }]));

        var wrongCard = ModernoTheme.Definition with { Name = "errado", ProductCard = typeof(Themes.Moderno.HomeContent) };
        Should.Throw<InvalidOperationException>(() => new ThemeCatalog(ClassicoTheme.Definition, [wrongCard]))
            .Message.ShouldContain("ProductCard");
    }

    private BrowserSession NewBrowser() => new(postgres.Web.CreateClient());

    private static string FirstProductUrl(string listingHtml) => ProductLink().Match(listingHtml).Value;

    [GeneratedRegex("/produtos/[a-z0-9-]+(?=\")")]
    private static partial Regex ProductLink();

    // Scoped CSS adds an attribute (b-…) after aria-current.
    [GeneratedRegex("aria-current=\"true\"[^>]*>(?<name>[^<]+)</a>")]
    private static partial Regex CurrentInSwitcher();
}
