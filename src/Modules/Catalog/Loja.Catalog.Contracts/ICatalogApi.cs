namespace Loja.Catalog.Contracts;

/// <summary>
/// What other modules may ask the Catalog. They never read its tables or use its domain types.
/// </summary>
public interface ICatalogApi
{
    /// <summary>
    /// Current data of the given variants, read from the database (never from the cache), so stock
    /// and price are up to date. Unknown ids are left out of the result.
    /// </summary>
    Task<IReadOnlyList<VariantSnapshot>> GetVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// A sellable variant as other modules see it at a given moment.
/// </summary>
/// <param name="ImageUrl">The picture that best represents this variant (e.g. its color).</param>
/// <param name="AvailableStock">Units in stock right now.</param>
/// <param name="IsPublished">False when the product was taken out of the store.</param>
public sealed record VariantSnapshot(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    string Currency,
    Uri? ImageUrl,
    int AvailableStock,
    bool IsPublished)
{
    public bool CanBePurchased => IsPublished && AvailableStock > 0;
}
