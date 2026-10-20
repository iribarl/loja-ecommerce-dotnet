using System.Net;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// Store pages rendered on the server, checked through their HTML.
/// </summary>
public sealed class CatalogPagesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Home_ListsCategories()
    {
        var (status, html) = await Get("/");

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Casacos e Moletons");
    }

    [Fact]
    public async Task Products_WithoutFilters_ShowsAllPublishedProducts()
    {
        var (status, html) = await Get("/produtos");

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("21 produtos");
        html.ShouldNotContain("Camiseta Edição Limitada");
    }

    [Fact]
    public async Task Products_WithCategoryAndAttribute_FiltersTheGrid()
    {
        var (_, html) = await Get("/produtos?categoria=acessorios&Tamanho=90");

        html.ShouldContain("Cinto de Couro");
        html.ShouldNotContain("Boné Aba Curva");
        html.ShouldContain("1 produto");
    }

    [Fact]
    public async Task Products_WithInvertedPriceRange_ShowsMessage()
    {
        var (status, html) = await Get("/produtos?min=100&max=50");

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("O preço mínimo não pode ser maior que o máximo.");
    }

    [Fact]
    public async Task ProductPage_ShowsProductWithPriceInReais()
    {
        var (status, html) = await Get("/produtos/moletom-canguru-com-capuz");

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Moletom Canguru com Capuz");
        html.ShouldContain("189,90");
        // Initial selection is the first variant in stock (Cinza Mescla), so its picture is shown.
        html.ShouldContain("/images/products/moletom-canguru-com-capuz-cinza-mescla.svg");
    }

    [Fact]
    public async Task ProductPage_WhenInitialVariantIsLowOnStock_ShowsLastUnits()
    {
        // Seed: the first variant of this product (REG-DRY-PRETO-P) has 2 units left.
        var (_, html) = await Get("/produtos/regata-dry-fit");

        html.ShouldContain("Últimas 2 unidades!");
    }

    [Theory]
    [InlineData("/produtos/nao-existe")]
    [InlineData("/produtos/camiseta-edicao-limitada")]
    public async Task ProductPage_UnknownOrDraft_Returns404Page(string url)
    {
        var (status, html) = await Get(url);

        status.ShouldBe(HttpStatusCode.NotFound);
        html.ShouldContain("Página não encontrada");
    }

    private async Task<(HttpStatusCode Status, string Html)> Get(string url)
    {
        var response = await postgres.Web.CreateClient().GetAsync(url, Ct);

        // Razor encodes accented letters (á → &#xE1;); decode to compare with plain text.
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Ct));
        return (response.StatusCode, html);
    }
}
