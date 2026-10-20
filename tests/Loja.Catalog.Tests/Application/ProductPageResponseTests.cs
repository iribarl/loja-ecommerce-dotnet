using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetProductBySlug;

namespace Loja.Catalog.Tests.Application;

public class ProductPageResponseTests
{
    private static readonly ProductImageDetails General = Image("/geral.svg");
    private static readonly ProductImageDetails Black = Image("/preto.svg", ("Cor", "Preto"));
    private static readonly ProductImageDetails BlackM = Image("/preto-m.svg", ("Cor", "Preto"), ("Tamanho", "M"));

    [Fact]
    public void ImageFor_PicksTheMostSpecificMatch()
    {
        var page = Page(General, Black, BlackM);

        page.ImageFor(Selection(("Cor", "Preto"), ("Tamanho", "M"))).ShouldBe(BlackM);
        page.ImageFor(Selection(("Cor", "Preto"), ("Tamanho", "G"))).ShouldBe(Black);
    }

    [Fact]
    public void ImageFor_WithoutMatch_FallsBackToGeneralImage()
    {
        Page(Black, General).ImageFor(Selection(("Cor", "Azul"))).ShouldBe(General);
    }

    [Fact]
    public void ImageFor_WithoutMatchOrGeneralImage_UsesTheFirstImage()
    {
        Page(Black).ImageFor(Selection(("Cor", "Azul"))).ShouldBe(Black);
    }

    [Fact]
    public void ImageFor_WithoutImages_IsNull()
    {
        Page().ImageFor(Selection(("Cor", "Azul"))).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(4, null)]
    public void LowStockUnits_IsOnlySetForLowStock(int stock, int? expected)
    {
        VariantDetails.LowStockUnits(stock).ShouldBe(expected);
    }

    private static ProductPageResponse Page(params ProductImageDetails[] images) =>
        new(new ProductDetails(Guid.NewGuid(), "Camiseta", "camiseta", "", "Camisetas", "camisetas", images, []),
            new Dictionary<string, IReadOnlyList<string>>());

    private static ProductImageDetails Image(string url, params (string Key, string Value)[] appliesTo) =>
        new(new Uri(url, UriKind.Relative), appliesTo.ToDictionary(p => p.Key, p => p.Value));

    private static Dictionary<string, string> Selection(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);
}
