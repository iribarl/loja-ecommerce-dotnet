using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products;

/// <summary>
/// Read side of the product catalog. Returns ready-to-display data, never domain entities.
/// Only published products are visible.
/// </summary>
public interface IProductQueries
{
    Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken);

    Task<ProductDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Attribute values present in published products, e.g. Tamanho: [P, M, G], to build search filters.
    /// Keys are sorted by name; values follow the catalog's display order.
    /// </summary>
    /// <param name="categorySlug">Restricts the values to one category; null for the whole catalog.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAttributeOptionsAsync(
        string? categorySlug,
        CancellationToken cancellationToken);
}

/// <summary>
/// Already validated and normalized search filters.
/// </summary>
public sealed record ProductSearchCriteria(
    string? Text,
    string? CategorySlug,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyDictionary<string, string> Attributes,
    bool InStockOnly,
    int Page,
    int PageSize);

/// <summary>A product as shown in a listing.</summary>
/// <param name="MinPrice">Lowest variant price ("a partir de").</param>
/// <param name="Images">Up to <see cref="MaxImages"/> pictures for the card, the cover first.</param>
public sealed record ProductSummary(
    Guid Id,
    string Name,
    string Slug,
    string CategoryName,
    decimal MinPrice,
    string Currency,
    IReadOnlyList<Uri> Images,
    bool InStock)
{
    public const int MaxImages = 5;

    public Uri? CoverImage => Images.Count > 0 ? Images[0] : null;
}

/// <summary>A product as shown on its own page.</summary>
public sealed record ProductDetails(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<ProductImageDetails> Images,
    IReadOnlyList<VariantDetails> Variants);

/// <param name="AppliesTo">Variant attributes the image shows, e.g. {"Cor": "Preto"}; empty for a general image.</param>
public sealed record ProductImageDetails(Uri Url, IReadOnlyDictionary<string, string> AppliesTo);

/// <param name="RemainingUnits">
/// Set only when stock is low (see <see cref="LowStockThreshold"/>), to show "Últimas N unidades".
/// The exact stock is not exposed otherwise.
/// </param>
public sealed record VariantDetails(
    Guid Id,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    string Currency,
    bool InStock,
    int? RemainingUnits)
{
    public const int LowStockThreshold = 3;

    public static int? LowStockUnits(int stock) => stock is > 0 and <= LowStockThreshold ? stock : null;
}
