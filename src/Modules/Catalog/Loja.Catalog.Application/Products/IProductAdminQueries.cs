using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products;

/// <summary>
/// Read side for the admin panel: every product, published or not, with the real stock.
/// Never cached: the panel must show what is in the database.
/// </summary>
public interface IProductAdminQueries
{
    Task<PagedResult<AdminProductRow>> ListAsync(AdminProductFilter filter, CancellationToken cancellationToken);

    Task<AdminProduct?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The variant's latest stock adjustments, newest first.</summary>
    Task<IReadOnlyList<AdminStockAdjustment>> ListStockAdjustmentsAsync(Guid variantId, int count, CancellationToken cancellationToken);
}

public sealed record AdminStockAdjustment(int Quantity, string Reason, int StockAfter, DateTimeOffset AdjustedAt);

/// <param name="Published">Null for all products.</param>
public sealed record AdminProductFilter(string? Text, bool? Published, int Page, int PageSize);

public sealed record AdminProductRow(
    Guid Id,
    string Name,
    string Slug,
    string CategoryName,
    bool IsPublished,
    int VariantCount,
    int TotalStock,
    decimal? MinPrice,
    string Currency);

public sealed record AdminProduct(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    Guid CategoryId,
    bool IsPublished,
    IReadOnlyList<AdminVariant> Variants);

public sealed record AdminVariant(
    Guid Id,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal Price,
    string Currency,
    int Stock);
