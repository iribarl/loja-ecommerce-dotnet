using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Tests.Domain;

public class VariantAttributesTests
{
    [Fact]
    public void Equality_IgnoresInsertionOrder()
    {
        var first = Attributes(("Tamanho", "M"), ("Cor", "Azul"));
        var second = Attributes(("Cor", "Azul"), ("Tamanho", "M"));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_DetectsDifferentValues()
    {
        Attributes(("Tamanho", "M")).ShouldNotBe(Attributes(("Tamanho", "G")));
    }

    [Theory]
    [InlineData("", "M")]
    [InlineData("Tamanho", " ")]
    public void Create_WithEmptyKeyOrValue_Fails(string key, string value)
    {
        VariantAttributes.Create([new(key, value)]).Error.ShouldBe(CatalogErrors.InvalidAttribute);
    }

    [Fact]
    public void Create_WithDuplicateKeyAfterTrimming_Fails()
    {
        VariantAttributes.Create([new("Cor", "Azul"), new(" Cor ", "Preto")])
            .Error.ShouldBe(CatalogErrors.InvalidAttribute);
    }

    private static VariantAttributes Attributes(params (string Key, string Value)[] pairs) =>
        VariantAttributes.Create(pairs.Select(p => KeyValuePair.Create(p.Key, p.Value))).Value;
}
