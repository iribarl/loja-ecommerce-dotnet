using Microsoft.Extensions.Caching.Hybrid;

namespace Loja.Catalog.Infrastructure.Caching;

internal static class CatalogCache
{
    /// <summary>
    /// Every catalog entry carries this tag, so any catalog change can evict them all at once
    /// with <c>HybridCache.RemoveByTagAsync(CatalogCache.Tag)</c>.
    /// </summary>
    public const string Tag = "catalog";

    public static readonly string[] Tags = [Tag];

    // Redis keeps entries for 5 minutes; each app instance keeps its in-memory copy for 1 minute,
    // so several instances converge quickly. Stock shown from the cache is informative only:
    // the cart and checkout always check stock against the database.
    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };
}
