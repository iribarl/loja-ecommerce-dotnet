using Loja.SharedKernel;

namespace Loja.Shipping.Domain;

/// <summary>A group of CEP regions with the same price and delivery time.</summary>
/// <param name="Name">Shown to the shopper, e.g. "Sudeste".</param>
public sealed record ShippingZone(string Name, Money Price, int MinDays, int MaxDays);

/// <summary>A shipping price for a destination.</summary>
public sealed record ShippingQuoteResult(ShippingZone Zone, Money Price, bool IsFree);

/// <summary>
/// Fixed shipping table (MVP). The store ships from São Paulo; the first digit of the CEP tells the
/// region of Brazil, so the price grows with the distance. Orders from <see cref="FreeShippingFrom"/>
/// on ship for free.
/// </summary>
/// <remarks>Real carrier prices (Correios or aggregators) come after the MVP.</remarks>
public static class ShippingRates
{
    public static readonly Money FreeShippingFrom = Money.Of(299m);

    private static readonly ShippingZone CapitalSp = new("São Paulo (capital e região)", Money.Of(12.90m), 1, 2);
    private static readonly ShippingZone InteriorSp = new("São Paulo (interior)", Money.Of(16.90m), 2, 3);
    private static readonly ShippingZone SoutheastAndSouth = new("Sudeste e Sul", Money.Of(19.90m), 3, 5);
    private static readonly ShippingZone FarSouthAndCenter = new("Nordeste, Centro-Oeste e Rio Grande do Sul", Money.Of(24.90m), 5, 8);
    private static readonly ShippingZone NorthAndNortheast = new("Norte e Nordeste (CE, PI, MA)", Money.Of(32.90m), 7, 12);

    // Index = first digit of the CEP (Correios regions).
    private static readonly ShippingZone[] ZoneByFirstDigit =
    [
        CapitalSp,          // 0: São Paulo capital and metropolitan area
        InteriorSp,         // 1: São Paulo interior
        SoutheastAndSouth,  // 2: Rio de Janeiro, Espírito Santo
        SoutheastAndSouth,  // 3: Minas Gerais
        FarSouthAndCenter,  // 4: Bahia, Sergipe
        FarSouthAndCenter,  // 5: Pernambuco, Alagoas, Paraíba, Rio Grande do Norte
        NorthAndNortheast,  // 6: Ceará, Piauí, Maranhão and the North
        FarSouthAndCenter,  // 7: Distrito Federal, Goiás, Tocantins, Mato Grosso (do Sul), Rondônia
        SoutheastAndSouth,  // 8: Paraná, Santa Catarina
        FarSouthAndCenter,  // 9: Rio Grande do Sul
    ];

    public static ShippingZone ZoneFor(Cep destination) => ZoneByFirstDigit[destination.FirstDigit];

    public static ShippingQuoteResult Quote(Cep destination, Money subtotal)
    {
        var zone = ZoneFor(destination);
        var isFree = subtotal.Amount >= FreeShippingFrom.Amount;

        return new ShippingQuoteResult(zone, isFree ? Money.Zero(zone.Price.Currency) : zone.Price, isFree);
    }
}
