using Loja.Catalog.Application.Categories;
using Microsoft.Extensions.Caching.Hybrid;

namespace Loja.Catalog.Infrastructure.Caching;

/// <summary>
/// Decorator over the category queries. See <see cref="CachedProductQueries"/>.
/// </summary>
internal sealed class CachedCategoryQueries(ICategoryQueries inner, HybridCache cache) : ICategoryQueries
{
    public Task<IReadOnlyList<CategorySummary>> ListAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            "catalog:categories",
            inner,
            static (queries, ct) => new ValueTask<IReadOnlyList<CategorySummary>>(queries.ListAsync(ct)),
            CatalogCache.EntryOptions,
            CatalogCache.Tags,
            cancellationToken).AsTask();
}
