using Loja.Catalog.Application.Products;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Catalog.Infrastructure.Queries;
using Loja.SharedKernel;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// Runs the real queries against the demo seed (see CatalogSeedData).
/// </summary>
public sealed class ProductQueriesTests(PostgresFixture postgres)
{
    private const int PublishedProducts = 21;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_WithoutFilters_ReturnsOnlyPublishedProducts()
    {
        var result = await Search(Criteria());

        result.TotalCount.ShouldBe(PublishedProducts);
        result.Items.ShouldNotContain(p => p.Name == "Camiseta Edição Limitada");
    }

    [Fact]
    public async Task Search_ByText_UnderstandsPortuguesePlurals()
    {
        var result = await Search(Criteria(text: "camisetas"));

        result.Items.Select(p => p.Name).ShouldBe(
            ["Camiseta Básica Algodão", "Camiseta Estampada Tropical", "Camiseta Manga Longa"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Search_ByCategory_ReturnsItsProducts()
    {
        var result = await Search(Criteria(category: "acessorios"));

        result.Items.Select(p => p.Name).ShouldBe(
            ["Boné Aba Curva", "Cinto de Couro", "Kit 3 Meias Cano Alto"]);
    }

    [Fact]
    public async Task Search_ByUnknownCategory_ReturnsNothing()
    {
        (await Search(Criteria(category: "nao-existe"))).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Search_ByAttribute_MatchesVariantAttributes()
    {
        var result = await Search(Criteria(attributes: new() { ["Tamanho"] = "44" }));

        result.Items.Select(p => p.Name).ShouldBe(["Bermuda Jeans", "Calça Chino", "Calça Jeans Slim"]);
    }

    [Fact]
    public async Task Search_ByTwoAttributes_RequiresTheSameVariantToMatchBoth()
    {
        var result = await Search(Criteria(attributes: new() { ["Tamanho"] = "GG", ["Cor"] = "Amarelo" }));

        result.Items.ShouldHaveSingleItem().Name.ShouldBe("Jaqueta Corta-Vento");
    }

    [Fact]
    public async Task Search_ByPriceRange_UsesVariantPrices()
    {
        var result = await Search(Criteria(minPrice: 30m, maxPrice: 50m));

        result.Items.Select(p => p.Name).ShouldBe(["Camiseta Básica Algodão", "Kit 3 Meias Cano Alto", "Regata Dry Fit"]);
        result.Items.ShouldAllBe(p => p.MinPrice >= 30m && p.MinPrice <= 50m);
    }

    [Fact]
    public async Task Search_InStockOnly_ExcludesSoldOutProducts()
    {
        var all = await Search(Criteria(pageSize: 48));
        var inStock = await Search(Criteria(pageSize: 48, inStockOnly: true));

        inStock.Items.ShouldAllBe(p => p.InStock);
        inStock.TotalCount.ShouldBe(all.Items.Count(p => p.InStock));
    }

    [Fact]
    public async Task Search_Paging_ReturnsTheRequestedPageOrderedByName()
    {
        var all = await Search(Criteria(pageSize: 48));
        var page2 = await Search(Criteria(page: 2, pageSize: 5));

        page2.TotalCount.ShouldBe(PublishedProducts);
        page2.TotalPages.ShouldBe(5);
        page2.Items.Select(p => p.Name).ShouldBe(all.Items.Skip(5).Take(5).Select(p => p.Name));
    }

    [Fact]
    public async Task Search_Summary_HasCategoryPriceAndCoverImage()
    {
        var result = await Search(Criteria(text: "polo"));

        var polo = result.Items.ShouldHaveSingleItem();
        polo.CategoryName.ShouldBe("Camisetas");
        polo.MinPrice.ShouldBe(89.90m);
        polo.Currency.ShouldBe("BRL");
        // No general image in the seed: the cover is the first color's image.
        polo.CoverImage!.OriginalString.ShouldBe("/images/products/camisa-polo-piquet-azul.svg");
    }

    [Fact]
    public async Task GetBySlug_ReturnsOneImagePerColor()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var product = await new ProductQueries(db).GetBySlugAsync("jaqueta-corta-vento", Ct);

        product!.Images.Select(i => (i.Url.OriginalString, i.AppliesTo[CatalogSeeder.ColorAttribute])).ShouldBe(
        [
            ("/images/products/jaqueta-corta-vento-preto.svg", "Preto"),
            ("/images/products/jaqueta-corta-vento-amarelo.svg", "Amarelo"),
        ]);
    }

    [Fact]
    public async Task GetBySlug_ExposesRemainingUnitsOnlyWhenStockIsLow()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var product = await new ProductQueries(db).GetBySlugAsync("jaqueta-corta-vento", Ct);

        // Seed stock for this product: PRETO P=12 M=4 G=9 GG=1, AMARELO P=6 M=11 G=3 GG=8.
        var remaining = product!.Variants.ToDictionary(v => v.Sku, v => v.RemainingUnits);
        remaining["JAQ-CVE-PRETO-GG"].ShouldBe(1);
        remaining["JAQ-CVE-AMARELO-G"].ShouldBe(3);
        remaining["JAQ-CVE-PRETO-P"].ShouldBeNull();
        remaining["JAQ-CVE-PRETO-M"].ShouldBeNull();
    }

    [Fact]
    public async Task GetBySlug_ReturnsVariantsInCatalogOrder()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var product = await new ProductQueries(db).GetBySlugAsync("moletom-canguru-com-capuz", Ct);

        product.ShouldNotBeNull();
        product.CategorySlug.ShouldBe("casacos-e-moletons");
        product.Variants.Count.ShouldBe(12);
        product.Variants.Take(4).Select(v => v.Attributes[CatalogSeeder.SizeAttribute]).ShouldBe(["P", "M", "G", "GG"]);
        product.Variants[0].Sku.ShouldBe("MOL-CAN-CINZA-MESCLA-P");
    }

    [Fact]
    public async Task GetBySlug_ForDraftProduct_ReturnsNull()
    {
        await using var db = postgres.CreateCatalogDbContext();

        (await new ProductQueries(db).GetBySlugAsync("camiseta-edicao-limitada", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task AttributeOptions_ForWholeCatalog_FollowDisplayOrder()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var options = await new ProductQueries(db).GetAttributeOptionsAsync(null, Ct);

        options.Keys.ShouldBe(["Cor", "Tamanho"]);
        options["Tamanho"].Count.ShouldBe(13);
        options["Cor"].Count.ShouldBe(16);
    }

    [Fact]
    public async Task AttributeOptions_ForCategory_OnlyIncludeItsValues()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var options = await new ProductQueries(db).GetAttributeOptionsAsync("camisetas", Ct);

        // Sizes keep the catalog order; for colors only the set matters.
        options["Tamanho"].ShouldBe(["P", "M", "G", "GG"]);
        options["Cor"].ShouldBe(
            ["Azul", "Azul Marinho", "Branco", "Cinza", "Preto", "Verde", "Vermelho"],
            ignoreOrder: true);
    }

    private async Task<PagedResult<ProductSummary>> Search(ProductSearchCriteria criteria)
    {
        await using var db = postgres.CreateCatalogDbContext();
        return await new ProductQueries(db).SearchAsync(criteria, Ct);
    }

    private static ProductSearchCriteria Criteria(
        string? text = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        Dictionary<string, string>? attributes = null,
        bool inStockOnly = false,
        int page = 1,
        int pageSize = 12) =>
        new(text, category, minPrice, maxPrice, attributes ?? [], inStockOnly, page, pageSize);
}
