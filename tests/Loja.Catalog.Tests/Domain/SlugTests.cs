using Loja.Catalog.Domain;

namespace Loja.Catalog.Tests.Domain;

public class SlugTests
{
    [Theory]
    [InlineData("Camiseta Básica", "camiseta-basica")]
    [InlineData("  Calça Jeans — Slim Fit!  ", "calca-jeans-slim-fit")]
    [InlineData("Moletom c/ Capuz", "moletom-c-capuz")]
    [InlineData("Tênis 2026", "tenis-2026")]
    public void From_NormalizesText(string text, string expected)
    {
        Slug.From(text).Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void From_WithoutLettersOrDigits_Fails(string text)
    {
        Slug.From(text).Error.ShouldBe(CatalogErrors.InvalidSlug);
    }
}
