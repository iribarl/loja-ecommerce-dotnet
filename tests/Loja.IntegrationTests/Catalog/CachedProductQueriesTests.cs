using Loja.Catalog.Application.Products;
using Loja.Catalog.Infrastructure.Caching;
using Loja.SharedKernel;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// The cache decorator with a real in-memory HybridCache and counting fake queries.
/// </summary>
public sealed class CachedProductQueriesTests : IAsyncDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly CountingProductQueries _inner = new();
    private readonly CachedProductQueries _cached;

    public CachedProductQueriesTests() =>
        _cached = new CachedProductQueries(_inner, _services.GetRequiredService<HybridCache>());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SameSearchTwice_QueriesOnce()
    {
        var first = await _cached.SearchAsync(Criteria(), Ct);
        var second = await _cached.SearchAsync(Criteria(), Ct);

        // HybridCache hands out a deserialized copy (not the same instance), so callers
        // cannot accidentally modify the cached value; what matters is the single query.
        _inner.SearchCalls.ShouldBe(1);
        second.TotalCount.ShouldBe(first.TotalCount);
    }

    [Fact]
    public async Task DifferentPage_QueriesAgain()
    {
        await _cached.SearchAsync(Criteria(page: 1), Ct);
        await _cached.SearchAsync(Criteria(page: 2), Ct);

        _inner.SearchCalls.ShouldBe(2);
    }

    [Fact]
    public void SearchKey_IgnoresTextCaseAndAttributeOrder()
    {
        var first = Criteria(text: "Camiseta", attributes: new() { ["Tamanho"] = "M", ["Cor"] = "Azul" });
        var second = Criteria(text: "camiseta", attributes: new() { ["Cor"] = "Azul", ["Tamanho"] = "M" });

        CachedProductQueries.SearchKey(first).ShouldBe(CachedProductQueries.SearchKey(second));
    }

    [Fact]
    public void SearchKey_DistinguishesEveryFilter()
    {
        string[] keys =
        [
            CachedProductQueries.SearchKey(Criteria()),
            CachedProductQueries.SearchKey(Criteria(text: "polo")),
            CachedProductQueries.SearchKey(Criteria(category: "camisetas")),
            CachedProductQueries.SearchKey(Criteria(minPrice: 10)),
            CachedProductQueries.SearchKey(Criteria(maxPrice: 10)),
            CachedProductQueries.SearchKey(Criteria(inStockOnly: true)),
            CachedProductQueries.SearchKey(Criteria(attributes: new() { ["Cor"] = "Azul" })),
            CachedProductQueries.SearchKey(Criteria(pageSize: 24)),
        ];

        keys.Distinct().Count().ShouldBe(keys.Length);
    }

    [Fact]
    public async Task UnknownSlug_IsCachedToo()
    {
        (await _cached.GetBySlugAsync("nao-existe", Ct)).ShouldBeNull();
        (await _cached.GetBySlugAsync("nao-existe", Ct)).ShouldBeNull();

        _inner.GetBySlugCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RemovingTheCatalogTag_EvictsEntries()
    {
        await _cached.SearchAsync(Criteria(), Ct);

        await _services.GetRequiredService<HybridCache>().RemoveByTagAsync(CatalogCache.Tag, Ct);
        await _cached.SearchAsync(Criteria(), Ct);

        _inner.SearchCalls.ShouldBe(2);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private static ProductSearchCriteria Criteria(
        string? text = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        Dictionary<string, string>? attributes = null,
        bool inStockOnly = false,
        int page = 1,
        int pageSize = 12) =>
        new(text, category, minPrice, maxPrice, attributes ?? [], inStockOnly, page, pageSize);

    private sealed class CountingProductQueries : IProductQueries
    {
        public int SearchCalls { get; private set; }

        public int GetBySlugCalls { get; private set; }

        public Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken)
        {
            SearchCalls++;
            return Task.FromResult(new PagedResult<ProductSummary>([], criteria.Page, criteria.PageSize, 0));
        }

        public Task<ProductDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
        {
            GetBySlugCalls++;
            return Task.FromResult<ProductDetails?>(null);
        }

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAttributeOptionsAsync(
            string? categorySlug,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(new Dictionary<string, IReadOnlyList<string>>());
    }
}
