using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.ManageProducts;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Tests.Application;

public sealed class ManageProductsTests
{
    private static readonly Guid Category = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeProductRepository _products = new(new CategoryId(Category));
    private readonly FakeCache _cache = new();

    [Fact]
    public async Task Create_SavesADraft_AndDropsTheCache()
    {
        var id = await new CreateProductHandler(_products, _cache).HandleAsync(new("Camiseta Nova", " Algodão ", Category), Ct);

        var product = _products.Saved.ShouldHaveSingleItem();
        product.Id.Value.ShouldBe(id.Value);
        product.Slug.Value.ShouldBe("camiseta-nova");
        product.IsPublished.ShouldBeFalse();
        _cache.Invalidations.ShouldBe(1);
    }

    [Fact]
    public async Task Create_WithTheNameOfAnotherProduct_GetsTheNextFreeSlug()
    {
        var handler = new CreateProductHandler(_products, _cache);
        await handler.HandleAsync(new("Camiseta", "", Category), Ct);
        await handler.HandleAsync(new("Camiseta", "", Category), Ct);
        await handler.HandleAsync(new("Camiseta", "", Category), Ct);

        _products.Saved.Select(p => p.Slug.Value).ShouldBe(["camiseta", "camiseta-2", "camiseta-3"]);
    }

    [Fact]
    public async Task Create_InUnknownCategory_Fails()
    {
        var result = await new CreateProductHandler(_products, _cache).HandleAsync(new("Camiseta", "", Guid.NewGuid()), Ct);

        result.Error.ShouldBe(CatalogErrors.CategoryNotFound);
        _products.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Publish_WithoutVariants_Fails()
    {
        var product = await NewProduct();
        var invalidations = _cache.Invalidations;

        var result = await new PublishProductHandler(_products, _cache).HandleAsync(product.Id.Value, publish: true, Ct);

        result.Error.ShouldBe(CatalogErrors.PublishWithoutVariants);
        _cache.Invalidations.ShouldBe(invalidations);
    }

    [Fact]
    public async Task AddVariant_ParsesAttributes_AndAddsIt()
    {
        var product = await NewProduct();

        var result = await new AddVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, new("cam-az-m", "Tamanho: M\nCor: Azul", 59.90m, 3), Ct);

        var variant = product.Variants.ShouldHaveSingleItem();
        variant.Id.Value.ShouldBe(result.Value);
        variant.Sku.Value.ShouldBe("CAM-AZ-M");
        variant.Attributes.Values.ShouldBe(new Dictionary<string, string> { ["Cor"] = "Azul", ["Tamanho"] = "M" });
        variant.Price.ShouldBe(Money.Of(59.90m));
        variant.Stock.ShouldBe(3);
    }

    [Fact]
    public async Task AddVariant_WithASkuOfAnotherProduct_Fails()
    {
        _products.SkusElsewhere.Add("CAM-AZ-M");
        var product = await NewProduct();

        var result = await new AddVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, new("CAM-AZ-M", "Tamanho: M", 59.90m, 3), Ct);

        result.Error.ShouldBe(CatalogErrors.SkuInUse);
    }

    [Fact]
    public async Task UpdateVariant_ChangesPriceAndAttributes()
    {
        var product = await NewProduct();
        var variant = await new AddVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, new("CAM-M", "Tamanho: M", 59.90m, 3), Ct);

        var result = await new UpdateVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, variant.Value, new("Tamanho: GG", 69.90m), Ct);

        result.IsSuccess.ShouldBeTrue();
        product.Variants[0].Price.ShouldBe(Money.Of(69.90m));
        product.Variants[0].Attributes.Values["Tamanho"].ShouldBe("GG");
    }

    [Fact]
    public async Task UpdateVariant_WithZeroPrice_SavesNothing()
    {
        var product = await NewProduct();
        var variant = await new AddVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, new("CAM-M", "Tamanho: M", 59.90m, 3), Ct);
        var saves = _products.SaveCount;

        var result = await new UpdateVariantHandler(_products, _cache)
            .HandleAsync(product.Id.Value, variant.Value, new("Tamanho: GG", 0m), Ct);

        result.Error.ShouldBe(CatalogErrors.InvalidPrice);
        _products.SaveCount.ShouldBe(saves);
    }

    [Theory]
    [InlineData("Tamanho: M\r\nCor:  Azul ", 2)]
    [InlineData("", 0)]
    [InlineData("  \n ", 0)]
    public void AttributesText_Parses(string text, int count)
    {
        AttributesText.Parse(text).Value.Values.Count.ShouldBe(count);
    }

    [Theory]
    [InlineData("Tamanho M")]
    [InlineData("Tamanho:")]
    [InlineData("Tamanho: M\nTamanho: G")]
    public void AttributesText_RejectsMalformedLines(string text)
    {
        AttributesText.Parse(text).Error.ShouldBe(CatalogErrors.InvalidAttribute);
    }

    [Fact]
    public void AttributesText_FormatsOnePairPerLine()
    {
        AttributesText.Format(new Dictionary<string, string> { ["Cor"] = "Azul", ["Tamanho"] = "M" }).ShouldBe("Cor: Azul\nTamanho: M");
    }

    private async Task<Product> NewProduct()
    {
        var id = await new CreateProductHandler(_products, _cache).HandleAsync(new("Camiseta", "", Category), Ct);
        return _products.Saved.Single(p => p.Id.Value == id.Value);
    }
}
