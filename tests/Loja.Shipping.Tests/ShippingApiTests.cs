using Loja.Shipping.Contracts;
using Loja.Shipping.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loja.Shipping.Tests;

/// <summary>The public contract, resolved from DI as other modules get it.</summary>
public class ShippingApiTests
{
    private readonly IShippingApi _shipping;

    public ShippingApiTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddShippingModule();
        _shipping = builder.Build().Services.GetRequiredService<IShippingApi>();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Quote_NormalizesTheCep_AndDescribesTheDelivery()
    {
        var quote = await _shipping.QuoteAsync("30130000", 150m, "BRL", Ct);

        quote.ShouldNotBeNull();
        quote.Cep.ShouldBe("30130-000");
        quote.Region.ShouldBe("Sudeste e Sul");
        quote.Price.ShouldBe(19.90m);
        quote.MinDays.ShouldBe(3);
        quote.MaxDays.ShouldBe(5);
        quote.FreeShippingFrom.ShouldBe(299m);
    }

    [Fact]
    public async Task Quote_WithInvalidCep_IsNull()
    {
        (await _shipping.QuoteAsync("123", 150m, "BRL", Ct)).ShouldBeNull();
    }
}
