using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Tests.Domain;

public class SkuTests
{
    [Theory]
    [InlineData("CAM-BAS-AZ-M", "CAM-BAS-AZ-M")]
    [InlineData(" cam-bas-az-m ", "CAM-BAS-AZ-M")]
    [InlineData("SKU123", "SKU123")]
    public void Create_WithValidValue_NormalizesToUppercase(string value, string expected)
    {
        Sku.Create(value).Value.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CAM BAS")]
    [InlineData("-CAM")]
    [InlineData("CAM--BAS")]
    [InlineData("CAMISETA_AZUL")]
    public void Create_WithInvalidValue_Fails(string value)
    {
        Sku.Create(value).Error.ShouldBe(CatalogErrors.InvalidSku);
    }

    [Fact]
    public void Create_LongerThanMaxLength_Fails()
    {
        Sku.Create(new string('A', Sku.MaxLength + 1)).Error.ShouldBe(CatalogErrors.InvalidSku);
    }
}
