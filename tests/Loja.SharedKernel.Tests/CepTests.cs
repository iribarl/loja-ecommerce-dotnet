namespace Loja.SharedKernel.Tests;

public class CepTests
{
    [Theory]
    [InlineData("01310-100", "01310100")]
    [InlineData("01310100", "01310100")]
    [InlineData("  90010-000 ", "90010000")]
    public void Create_AcceptsCommonFormats(string value, string digits)
    {
        Cep.Create(value).Value.Digits.ShouldBe(digits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1310-100")]
    [InlineData("013101000")]
    [InlineData("01310 100")]
    [InlineData("0131A-100")]
    [InlineData("013-10100")]
    [InlineData("00000-000")]
    public void Create_RejectsInvalidValues(string? value)
    {
        Cep.Create(value).Error.ShouldBe(Cep.Invalid);
    }

    [Fact]
    public void ToString_UsesTheUsualFormat()
    {
        Cep.Create("01310100").Value.ToString().ShouldBe("01310-100");
    }

    [Fact]
    public void FirstDigit_IdentifiesTheRegion()
    {
        Cep.Create("90010-000").Value.FirstDigit.ShouldBe(9);
    }
}
