using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetProductBySlug;

namespace Loja.Catalog.Tests.Application;

public class GetProductBySlugHandlerTests
{
    private readonly FakeProductQueries _queries = new();
    private readonly GetProductBySlugHandler _handler;

    public GetProductBySlugHandlerTests() => _handler = new GetProductBySlugHandler(_queries);

    [Theory]
    [InlineData("")]
    [InlineData("nao-existe")]
    public async Task UnknownSlug_ReturnsNotFound(string slug)
    {
        var result = await _handler.HandleAsync(slug, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(GetProductBySlugHandler.ProductNotFound);
    }

    [Fact]
    public async Task AttributeOptions_AreDistinct_AndKeepVariantOrder()
    {
        _queries.ProductsBySlug["camiseta"] = Product(
            Variant("P", "Azul"),
            Variant("M", "Azul"),
            Variant("G", "Azul"),
            Variant("P", "Preto"),
            Variant("M", "Preto"));

        var result = await _handler.HandleAsync("camiseta", TestContext.Current.CancellationToken);

        var options = result.Value.AttributeOptions;
        options["Tamanho"].ShouldBe(["P", "M", "G"]);
        options["Cor"].ShouldBe(["Azul", "Preto"]);
    }

    private static ProductDetails Product(params VariantDetails[] variants) =>
        new(Guid.NewGuid(), "Camiseta", "camiseta", "", "Camisetas", "camisetas", [], variants);

    private static VariantDetails Variant(string size, string color) =>
        new(
            Guid.NewGuid(),
            $"CAM-{color}-{size}".ToUpperInvariant(),
            new Dictionary<string, string> { ["Tamanho"] = size, ["Cor"] = color },
            59.90m,
            "BRL",
            InStock: true);
}
