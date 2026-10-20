namespace Loja.Shipping.Contracts;

/// <summary>
/// What other modules may ask the Shipping module.
/// </summary>
public interface IShippingApi
{
    /// <summary>
    /// Shipping price and delivery time to a CEP for an order of <paramref name="subtotal"/>.
    /// Null when the CEP is invalid.
    /// </summary>
    /// <remarks>Asynchronous because a carrier API will answer it after the MVP.</remarks>
    Task<ShippingQuote?> QuoteAsync(string cep, decimal subtotal, string currency, CancellationToken cancellationToken);
}

/// <param name="Cep">Normalized, e.g. "01310-100".</param>
/// <param name="Region">Shown to the shopper, e.g. "Sudeste e Sul".</param>
public sealed record ShippingQuote(
    string Cep,
    string Region,
    decimal Price,
    string Currency,
    bool IsFree,
    int MinDays,
    int MaxDays,
    decimal FreeShippingFrom);
