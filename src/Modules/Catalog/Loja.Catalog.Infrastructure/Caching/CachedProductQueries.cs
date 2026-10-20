using System.Globalization;
using System.Text;
using Loja.Catalog.Application.Products;
using Loja.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;

namespace Loja.Catalog.Infrastructure.Caching;

/// <summary>
/// Decorator: same contract as the real queries, answered from the cache when possible.
/// The use cases do not know the cache exists.
/// </summary>
/// <remarks>
/// HybridCache keeps a local in-memory copy backed by Redis, and protects against stampedes:
/// if many requests miss the same key at once, only one of them runs the query.
/// </remarks>
internal sealed class CachedProductQueries(IProductQueries inner, HybridCache cache) : IProductQueries
{
    public Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            SearchKey(criteria),
            (inner, criteria),
            static (state, ct) => new ValueTask<PagedResult<ProductSummary>>(state.inner.SearchAsync(state.criteria, ct)),
            CatalogCache.EntryOptions,
            CatalogCache.Tags,
            cancellationToken).AsTask();

    // Unknown slugs are cached too (as null), so repeated requests for them do not reach the database.
    public Task<ProductDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"catalog:product:{slug}",
            (inner, slug),
            static (state, ct) => new ValueTask<ProductDetails?>(state.inner.GetBySlugAsync(state.slug, ct)),
            CatalogCache.EntryOptions,
            CatalogCache.Tags,
            cancellationToken).AsTask();

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAttributeOptionsAsync(
        string? categorySlug,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"catalog:attribute-options:{categorySlug}",
            (inner, categorySlug),
            static (state, ct) =>
                new ValueTask<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
                    state.inner.GetAttributeOptionsAsync(state.categorySlug, ct)),
            CatalogCache.EntryOptions,
            CatalogCache.Tags,
            cancellationToken).AsTask();

    /// <summary>
    /// One key per distinct search. Equivalent searches must produce the same key, so the text is
    /// case-normalized (the search ignores case) and attributes are sorted.
    /// </summary>
    internal static string SearchKey(ProductSearchCriteria criteria)
    {
        var key = new StringBuilder("catalog:search:");
        key.Append(CultureInfo.InvariantCulture, $"t={criteria.Text?.ToUpperInvariant()}");
        key.Append(CultureInfo.InvariantCulture, $"|c={criteria.CategorySlug}");
        key.Append(CultureInfo.InvariantCulture, $"|min={criteria.MinPrice}|max={criteria.MaxPrice}");
        key.Append(CultureInfo.InvariantCulture, $"|stock={criteria.InStockOnly}");

        foreach (var (name, value) in criteria.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            key.Append(CultureInfo.InvariantCulture, $"|a.{name}={value}");
        }

        key.Append(CultureInfo.InvariantCulture, $"|p={criteria.Page}|s={criteria.PageSize}");
        return key.ToString();
    }
}
