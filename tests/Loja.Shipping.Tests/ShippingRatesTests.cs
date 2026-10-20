using Loja.SharedKernel;
using Loja.Shipping.Domain;

namespace Loja.Shipping.Tests;

public class ShippingRatesTests
{
    [Theory]
    [InlineData("01310-100", 12.90)] // São Paulo capital
    [InlineData("13010-000", 16.90)] // Campinas (SP interior)
    [InlineData("20040-020", 19.90)] // Rio de Janeiro
    [InlineData("30130-000", 19.90)] // Belo Horizonte
    [InlineData("80010-000", 19.90)] // Curitiba
    [InlineData("40010-000", 24.90)] // Salvador
    [InlineData("70040-000", 24.90)] // Brasília
    [InlineData("90010-000", 24.90)] // Porto Alegre
    [InlineData("60010-000", 32.90)] // Fortaleza
    [InlineData("69005-000", 32.90)] // Manaus
    public void Quote_DependsOnTheRegionOfTheCep(string cep, decimal expected)
    {
        var quote = ShippingRates.Quote(Cep.Create(cep).Value, Money.Of(100m));

        quote.Price.ShouldBe(Money.Of(expected));
        quote.IsFree.ShouldBeFalse();
    }

    [Theory]
    [InlineData(298.99, false)]
    [InlineData(299.00, true)]
    [InlineData(500.00, true)]
    public void Quote_IsFreeFromTheThreshold(decimal subtotal, bool free)
    {
        var quote = ShippingRates.Quote(Cep.Create("60010-000").Value, Money.Of(subtotal));

        quote.IsFree.ShouldBe(free);
        quote.Price.Amount.ShouldBe(free ? 0m : 32.90m);
    }

    [Fact]
    public void FartherRegions_TakeLonger()
    {
        var capital = ShippingRates.ZoneFor(Cep.Create("01310-100").Value);
        var north = ShippingRates.ZoneFor(Cep.Create("69005-000").Value);

        north.MinDays.ShouldBeGreaterThan(capital.MaxDays);
    }
}
