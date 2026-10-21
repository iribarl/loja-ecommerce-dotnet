using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Application.Products;

/// <summary>
/// Write side of the products: loads and saves whole aggregates (product with its variants).
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(Slug slug, CancellationToken cancellationToken);

    /// <summary>SKUs are unique in the whole catalog, not only within one product.</summary>
    Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken);

    Task<bool> CategoryExistsAsync(CategoryId id, CancellationToken cancellationToken);

    void Add(Product product);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Drops the store's cached catalog pages, so a change made in the panel shows at once.
/// </summary>
public interface ICatalogCacheInvalidator
{
    Task InvalidateAsync(CancellationToken cancellationToken);
}
