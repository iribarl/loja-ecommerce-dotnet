namespace Loja.Cart.Application.GetCart;

/// <summary>
/// The cart as the shopper sees it, with current prices and stock from the catalog.
/// </summary>
/// <param name="Subtotal">Sum of the lines that can be bought (without shipping).</param>
/// <param name="CanCheckout">True when the cart has items and none of them has a problem.</param>
public sealed record CartView(
    IReadOnlyList<CartLineView> Lines,
    decimal Subtotal,
    string Currency,
    int TotalUnits,
    bool CanCheckout)
{
    public static readonly CartView Empty = new([], 0m, "BRL", 0, false);
}

/// <param name="MaxQuantity">Highest quantity the shopper may choose now (stock and per-item limit).</param>
/// <param name="Issue">Why the line cannot be bought as it is; null when it is fine.</param>
public sealed record CartLineView(
    Guid VariantId,
    string ProductName,
    string ProductSlug,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    Uri? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string Currency,
    int MaxQuantity,
    CartLineIssue? Issue);

public enum CartLineIssue
{
    /// <summary>The product was removed, unpublished or sold out.</summary>
    Unavailable,

    /// <summary>There is stock, but less than the quantity in the cart.</summary>
    NotEnoughStock,
}
