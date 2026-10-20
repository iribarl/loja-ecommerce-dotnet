using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Tests.Domain;

public class ProductTests
{
    private static readonly Sku SkuM = Sku.Create("CAM-AZ-M").Value;
    private static readonly Sku SkuG = Sku.Create("CAM-AZ-G").Value;
    private static readonly VariantAttributes SizeM = Attributes("M");
    private static readonly VariantAttributes SizeG = Attributes("G");
    private static readonly Money Price = Money.Of(59.90m);

    [Fact]
    public void Create_GeneratesSlugFromName_AndStartsAsDraft()
    {
        var product = NewProduct("Camiseta Básica");

        product.Slug.Value.ShouldBe("camiseta-basica");
        product.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithoutName_Fails()
    {
        Product.Create(" ", "", CategoryId.New()).Error.ShouldBe(CatalogErrors.NameRequired);
    }

    [Fact]
    public void AddVariant_Valid_AddsIt()
    {
        var product = NewProduct();

        var result = product.AddVariant(SkuM, SizeM, Price, stock: 10);

        result.IsSuccess.ShouldBeTrue();
        product.Variants.ShouldHaveSingleItem().Sku.ShouldBe(SkuM);
    }

    [Fact]
    public void AddVariant_AssignsPositionsInOrder()
    {
        var product = NewProduct();

        product.AddVariant(SkuM, SizeM, Price, 10);
        product.AddVariant(SkuG, SizeG, Price, 10);

        product.Variants.Select(v => v.Position).ShouldBe([0, 1]);
    }

    [Fact]
    public void AddVariant_WithDuplicateSku_Fails()
    {
        var product = NewProduct();
        product.AddVariant(SkuM, SizeM, Price, 10);

        product.AddVariant(SkuM, SizeG, Price, 10).Error.ShouldBe(CatalogErrors.DuplicateSku);
    }

    [Fact]
    public void AddVariant_WithDuplicateAttributes_Fails()
    {
        var product = NewProduct();
        product.AddVariant(SkuM, SizeM, Price, 10);

        product.AddVariant(SkuG, SizeM, Price, 10).Error.ShouldBe(CatalogErrors.DuplicateAttributes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddVariant_WithNonPositivePrice_Fails(decimal amount)
    {
        NewProduct().AddVariant(SkuM, SizeM, Money.Of(amount), 10).Error.ShouldBe(CatalogErrors.InvalidPrice);
    }

    [Fact]
    public void AddVariant_WithNegativeStock_Fails()
    {
        NewProduct().AddVariant(SkuM, SizeM, Price, -1).Error.ShouldBe(CatalogErrors.NegativeStock);
    }

    [Fact]
    public void SetStock_ChangesTheRightVariant()
    {
        var product = NewProduct();
        product.AddVariant(SkuM, SizeM, Price, 10);
        product.AddVariant(SkuG, SizeG, Price, 10);

        product.SetStock(SkuG, 0).IsSuccess.ShouldBeTrue();

        product.Variants.Single(v => v.Sku == SkuG).InStock.ShouldBeFalse();
        product.Variants.Single(v => v.Sku == SkuM).Stock.ShouldBe(10);
    }

    [Fact]
    public void SetStock_ForUnknownSku_Fails()
    {
        NewProduct().SetStock(SkuM, 5).Error.ShouldBe(CatalogErrors.VariantNotFound);
    }

    [Fact]
    public void ChangePrice_UpdatesVariantPrice()
    {
        var product = NewProduct();
        product.AddVariant(SkuM, SizeM, Price, 10);

        product.ChangePrice(SkuM, Money.Of(49.90m)).IsSuccess.ShouldBeTrue();

        product.Variants.Single().Price.ShouldBe(Money.Of(49.90m));
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.jpg")]
    [InlineData("/images/products/a.svg")]
    public void AddImage_WithHttpOrSitePath_AddsIt(string url)
    {
        var product = NewProduct();

        product.AddImage(new Uri(url, UriKind.RelativeOrAbsolute)).IsSuccess.ShouldBeTrue();

        product.Images.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddImage_ForSomeVariants_KeepsWhatItAppliesTo()
    {
        var product = NewProduct();
        var black = VariantAttributes.Create([KeyValuePair.Create("Cor", "Preto")]).Value;

        product.AddImage(new Uri("/a.svg", UriKind.Relative));
        product.AddImage(new Uri("/a-preto.svg", UriKind.Relative), black);

        product.Images[0].IsGeneral.ShouldBeTrue();
        product.Images[1].AppliesTo.ShouldBe(black);
    }

    [Fact]
    public void ImageFor_PicksTheImageOfTheVariantColor()
    {
        var product = NewProduct();
        product.AddImage(new Uri("/geral.svg", UriKind.Relative));
        product.AddImage(new Uri("/preto.svg", UriKind.Relative), Colors(("Cor", "Preto")));

        product.ImageFor(Colors(("Cor", "Preto"), ("Tamanho", "M")))!.Url.OriginalString.ShouldBe("/preto.svg");
        product.ImageFor(Colors(("Cor", "Azul"), ("Tamanho", "M")))!.Url.OriginalString.ShouldBe("/geral.svg");
    }

    [Fact]
    public void ImageFor_WithoutImages_IsNull()
    {
        NewProduct().ImageFor(SizeM).ShouldBeNull();
    }

    [Theory]
    [InlineData("images/a.svg")]
    [InlineData("//evil.example.com/a.svg")]
    [InlineData("file:///C:/a.svg")]
    [InlineData("javascript:alert(1)")]
    public void AddImage_WithOtherUrls_Fails(string url)
    {
        NewProduct().AddImage(new Uri(url, UriKind.RelativeOrAbsolute)).Error.ShouldBe(CatalogErrors.InvalidImageUrl);
    }

    [Fact]
    public void Publish_WithoutVariants_Fails()
    {
        NewProduct().Publish().Error.ShouldBe(CatalogErrors.PublishWithoutVariants);
    }

    [Fact]
    public void Publish_WithVariants_MakesItVisible()
    {
        var product = NewProduct();
        product.AddVariant(SkuM, SizeM, Price, 10);

        product.Publish().IsSuccess.ShouldBeTrue();

        product.IsPublished.ShouldBeTrue();
    }

    private static Product NewProduct(string name = "Camiseta") =>
        Product.Create(name, "Camiseta de algodão.", CategoryId.New()).Value;

    private static VariantAttributes Colors(params (string Key, string Value)[] pairs) =>
        VariantAttributes.Create(pairs.Select(p => KeyValuePair.Create(p.Key, p.Value))).Value;

    private static VariantAttributes Attributes(string size) =>
        VariantAttributes.Create([KeyValuePair.Create("Tamanho", size)]).Value;
}
