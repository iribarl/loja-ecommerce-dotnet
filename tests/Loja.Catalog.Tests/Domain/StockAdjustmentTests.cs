using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;

namespace Loja.Catalog.Tests.Domain;

public sealed class StockAdjustmentTests
{
    private static readonly ProductVariantId Variant = ProductVariantId.New();

    [Theory]
    [InlineData(10)]
    [InlineData(-2)]
    public void Create_Valid_KeepsEverything(int quantity)
    {
        var at = DateTimeOffset.UtcNow;
        var admin = Guid.NewGuid();

        var adjustment = StockAdjustment.Create(Variant, quantity, "  Reposição ", 8, admin, at).Value;

        adjustment.VariantId.ShouldBe(Variant);
        adjustment.Quantity.ShouldBe(quantity);
        adjustment.Reason.ShouldBe("Reposição");
        adjustment.StockAfter.ShouldBe(8);
        adjustment.AdjustedBy.ShouldBe(admin);
        adjustment.AdjustedAt.ShouldBe(at);
    }

    [Fact]
    public void Validate_ZeroQuantity_Fails() =>
        StockAdjustment.Validate(0, "Contagem").Error.ShouldBe(CatalogErrors.ZeroAdjustment);

    [Theory]
    [InlineData(StockAdjustment.MaxQuantity + 1)]
    [InlineData(-StockAdjustment.MaxQuantity - 1)]
    public void Validate_HugeQuantity_Fails(int quantity) =>
        StockAdjustment.Validate(quantity, "Contagem").Error.ShouldBe(CatalogErrors.AdjustmentTooLarge);

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_WithoutReason_Fails(string? reason) =>
        StockAdjustment.Validate(5, reason).Error.ShouldBe(CatalogErrors.AdjustmentReasonRequired);

    [Fact]
    public void Validate_TooLongReason_Fails() =>
        StockAdjustment.Validate(5, new string('a', StockAdjustment.MaxReasonLength + 1)).Error.ShouldBe(CatalogErrors.AdjustmentReasonRequired);

    [Fact]
    public void Create_WithNegativeResult_Fails() =>
        StockAdjustment.Create(Variant, -5, "Avaria", -1, Guid.NewGuid(), DateTimeOffset.UtcNow).Error.ShouldBe(CatalogErrors.NegativeStock);
}
