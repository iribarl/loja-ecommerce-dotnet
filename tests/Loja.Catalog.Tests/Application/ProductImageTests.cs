using Loja.Catalog.Application.Products.Images;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Tests.Application;

public sealed class ProductImageTests
{
    // The first bytes of each accepted format, followed by some content.
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 1, 2, 3, 4, 5, 6];
    private static readonly byte[] WebP = [.. "RIFF"u8, 1, 2, 3, 4, .. "WEBP"u8, 9];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly CategoryId _category = CategoryId.New();
    private readonly FakeImageStorage _storage = new();
    private readonly FakeCache _cache = new();
    private readonly FakeProductRepository _products;

    public ProductImageTests() => _products = new FakeProductRepository(_category);

    public static TheoryData<byte[], string?> Formats => new()
    {
        { Png, ".png" },
        { Jpeg, ".jpg" },
        { WebP, ".webp" },
        { "<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8.ToArray(), null },
        { "MZ executable"u8.ToArray(), null },
        { [], null },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void ImageFormat_IsDetectedByContent(byte[] bytes, string? extension) =>
        ImageFormat.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, ImageFormat.HeaderLength))).ShouldBe(extension);

    [Fact]
    public async Task Add_StoresTheFile_AndAddsItToTheProduct()
    {
        var product = NewProduct();

        var result = await Add(product, Png, "Cor: Preto");

        result.IsSuccess.ShouldBeTrue();
        var image = product.Images.ShouldHaveSingleItem();
        _storage.Files[image.Url].ShouldBe(Png);
        image.Url.OriginalString.ShouldEndWith(".png");
        image.AppliesTo.Values["Cor"].ShouldBe("Preto");
        _cache.Invalidations.ShouldBe(1);
    }

    [Fact]
    public async Task Add_NotAnImage_IsRefused_AndNothingIsStored()
    {
        var product = NewProduct();

        var result = await Add(product, "<html>oi</html>"u8.ToArray());

        result.Error.ShouldBe(CatalogErrors.UnsupportedImage);
        _storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_TooLarge_IsRefused()
    {
        var bytes = new byte[AddProductImageHandler.MaxBytes + 1];
        Png.CopyTo(bytes, 0);

        var result = await Add(NewProduct(), bytes);

        result.Error.ShouldBe(CatalogErrors.ImageTooLarge);
        _storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_RefusedByTheProduct_LeavesNoFileBehind()
    {
        var product = NewProduct();
        for (var i = 0; i < Product.MaxImages; i++)
        {
            product.AddImage(new Uri($"/images/products/{i}.svg", UriKind.Relative));
        }

        var result = await Add(product, Png);

        result.Error.ShouldBe(CatalogErrors.TooManyImages);
        _storage.Files.ShouldBeEmpty();
        _storage.Deleted.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Remove_TakesItOffTheProduct_AndDeletesTheFile()
    {
        var product = NewProduct();
        await Add(product, Png);
        var url = product.Images[0].Url;

        var result = await new RemoveProductImageHandler(_products, _storage, _cache).HandleAsync(product.Id.Value, url.OriginalString, Ct);

        result.IsSuccess.ShouldBeTrue();
        product.Images.ShouldBeEmpty();
        _storage.Deleted.ShouldContain(url);
    }

    [Fact]
    public void Messages_OnlyForKnownCodes() =>
        ProductImageMessages.For("<script>alert(1)</script>").ShouldBe("Não foi possível salvar a imagem.");

    private Task<Loja.SharedKernel.Result> Add(Product product, byte[] bytes, string? appliesTo = null) =>
        new AddProductImageHandler(_products, _storage, _cache).HandleAsync(product.Id.Value, new MemoryStream(bytes), appliesTo, Ct);

    private Product NewProduct()
    {
        var product = Product.Create($"Camiseta {Guid.NewGuid():N}", "", _category).Value;
        _products.Add(product);
        return product;
    }
}
