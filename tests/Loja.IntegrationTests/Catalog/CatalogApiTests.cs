using Loja.Catalog.Infrastructure.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// The Catalog's public contract, against the demo seed.
/// </summary>
public sealed class CatalogApiTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetVariants_ReturnsCurrentDataWithTheVariantImage()
    {
        var ids = await VariantIds("JAQ-CVE-PRETO-GG", "JAQ-CVE-AMARELO-M");
        await using var db = postgres.CreateCatalogDbContext();

        var variants = await new CatalogApi(db).GetVariantsAsync(ids.Values, Ct);

        var black = variants.Single(v => v.Sku == "JAQ-CVE-PRETO-GG");
        black.ProductName.ShouldBe("Jaqueta Corta-Vento");
        black.ProductSlug.ShouldBe("jaqueta-corta-vento");
        black.Price.ShouldBe(229.90m);
        black.AvailableStock.ShouldBe(1);
        black.CanBePurchased.ShouldBeTrue();
        black.ImageUrl!.OriginalString.ShouldBe("/images/products/jaqueta-corta-vento-preto.svg");

        variants.Single(v => v.Sku == "JAQ-CVE-AMARELO-M")
            .ImageUrl!.OriginalString.ShouldBe("/images/products/jaqueta-corta-vento-amarelo.svg");
    }

    [Fact]
    public async Task GetVariants_LeavesOutUnknownIds()
    {
        var ids = await VariantIds("CAM-BAS-PRETO-M");
        await using var db = postgres.CreateCatalogDbContext();

        var variants = await new CatalogApi(db).GetVariantsAsync([ids["CAM-BAS-PRETO-M"], Guid.NewGuid()], Ct);

        variants.ShouldHaveSingleItem().Sku.ShouldBe("CAM-BAS-PRETO-M");
    }

    [Fact]
    public async Task GetVariants_ForDraftProduct_CannotBePurchased()
    {
        var ids = await VariantIds("CAM-LIM-PRETO-M");
        await using var db = postgres.CreateCatalogDbContext();

        var variant = (await new CatalogApi(db).GetVariantsAsync(ids.Values, Ct)).ShouldHaveSingleItem();

        variant.IsPublished.ShouldBeFalse();
        variant.CanBePurchased.ShouldBeFalse();
    }

    [Fact]
    public async Task GetVariants_SoldOutVariant_CannotBePurchased()
    {
        var ids = await VariantIds("POL-PIQ-PRETO-M");
        await using var db = postgres.CreateCatalogDbContext();

        var variant = (await new CatalogApi(db).GetVariantsAsync(ids.Values, Ct)).ShouldHaveSingleItem();

        variant.AvailableStock.ShouldBe(0);
        variant.CanBePurchased.ShouldBeFalse();
    }

    // Variant ids are generated at seed time, so tests look them up by SKU.
    private async Task<Dictionary<string, Guid>> VariantIds(params string[] skus)
    {
        await using var db = postgres.CreateCatalogDbContext();
        var products = await db.Products.AsNoTracking().Include(p => p.Variants).ToListAsync(Ct);

        return products
            .SelectMany(p => p.Variants)
            .Where(v => skus.Contains(v.Sku.Value))
            .ToDictionary(v => v.Sku.Value, v => v.Id.Value);
    }
}
