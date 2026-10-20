using Loja.Catalog.Contracts;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Contracts;

/// <summary>
/// Implementation of the Catalog's public contract. Reads the database directly (no cache):
/// callers such as the cart and checkout decide with this stock.
/// </summary>
internal sealed class CatalogApi(CatalogDbContext db) : ICatalogApi
{
    public async Task<IReadOnlyList<VariantSnapshot>> GetVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        var ids = variantIds.Distinct().Select(id => new ProductVariantId(id)).ToList();

        // Whole aggregates: the domain knows which image represents each variant.
        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => p.Variants.Any(v => ids.Contains(v.Id)))
            .ToListAsync(cancellationToken);

        return products
            .SelectMany(product => product.Variants
                .Where(variant => ids.Contains(variant.Id))
                .Select(variant => new VariantSnapshot(
                    variant.Id.Value,
                    product.Id.Value,
                    product.Name,
                    product.Slug.Value,
                    variant.Sku.Value,
                    variant.Attributes.Values,
                    variant.Price.Amount,
                    variant.Price.Currency,
                    product.ImageFor(variant.Attributes)?.Url,
                    variant.Stock,
                    product.IsPublished)))
            .ToList();
    }
}
