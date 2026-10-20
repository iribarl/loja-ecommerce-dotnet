using Loja.SharedKernel;
using Loja.Shipping.Contracts;
using Loja.Shipping.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loja.Shipping.Infrastructure;

/// <summary>
/// Entry point of the Shipping module. No database: the MVP uses a fixed table.
/// </summary>
public static class ShippingModule
{
    public static IHostApplicationBuilder AddShippingModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IShippingApi, ShippingApi>();
        return builder;
    }
}

internal sealed class ShippingApi : IShippingApi
{
    public Task<ShippingQuote?> QuoteAsync(string cep, decimal subtotal, string currency, CancellationToken cancellationToken)
    {
        if (Cep.Create(cep) is not { IsSuccess: true } destination)
        {
            return Task.FromResult<ShippingQuote?>(null);
        }

        var quote = ShippingRates.Quote(destination.Value, Money.Of(subtotal, currency));

        return Task.FromResult<ShippingQuote?>(new ShippingQuote(
            destination.Value.ToString(),
            quote.Zone.Name,
            quote.Price.Amount,
            quote.Price.Currency,
            quote.IsFree,
            quote.Zone.MinDays,
            quote.Zone.MaxDays,
            ShippingRates.FreeShippingFrom.Amount));
    }
}
