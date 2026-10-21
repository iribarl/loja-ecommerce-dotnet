using Loja.Catalog.Application.Products;
using Microsoft.Extensions.Caching.Hybrid;

namespace Loja.Catalog.Infrastructure.Caching;

/// <summary>
/// Evicts every catalog entry (memory and Redis) by its tag. Other app instances keep their
/// in-memory copy for at most <see cref="CatalogCache.EntryOptions"/>' local expiration (1 minute).
/// </summary>
internal sealed class CatalogCacheInvalidator(HybridCache cache) : ICatalogCacheInvalidator
{
    public async Task InvalidateAsync(CancellationToken cancellationToken) =>
        await cache.RemoveByTagAsync(CatalogCache.Tag, cancellationToken);
}
