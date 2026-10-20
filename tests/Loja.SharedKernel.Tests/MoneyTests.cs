namespace Loja.SharedKernel.Tests;

public class MoneyTests
{
    [Fact]
    public void Of_WithoutCurrency_UsesBrl()
    {
        Money.Of(10m).Currency.ShouldBe("BRL");
    }

    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(-10.005, -10.01)]
    public void Of_RoundsToTwoDecimals_AwayFromZero(decimal amount, decimal expected)
    {
        Money.Of(amount).Amount.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("brl")]
    [InlineData("BR")]
    [InlineData("BRLX")]
    public void Of_WithInvalidCurrency_Throws(string currency)
    {
        Should.Throw<ArgumentException>(() => Money.Of(10m, currency));
    }

    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        (Money.Of(10.50m) + Money.Of(0.25m)).ShouldBe(Money.Of(10.75m));
    }

    [Fact]
    public void Subtract_SameCurrency_SubtractsAmounts()
    {
        (Money.Of(10m) - Money.Of(2.5m)).ShouldBe(Money.Of(7.5m));
    }

    [Fact]
    public void Multiply_ByQuantity_MultipliesAmount()
    {
        (Money.Of(19.90m) * 3).ShouldBe(Money.Of(59.70m));
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Money.Of(10m) + Money.Of(10m, "USD"));
    }

    [Fact]
    public void Equality_ComparesAmountAndCurrency()
    {
        Money.Of(10m).ShouldBe(Money.Of(10.00m));
        Money.Of(10m).ShouldNotBe(Money.Of(10m, "USD"));
    }

    [Fact]
    public void ToString_UsesInvariantFormat()
    {
        Money.Of(1234.5m).ToString().ShouldBe("1234.50 BRL");
    }
}
