using Loja.Catalog.Application.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Tests.Application;

/// <summary>
/// Hand-written fake: records the criteria it receives and returns canned data.
/// </summary>
internal sealed class FakeProductQueries : IProductQueries
{
    public ProductSearchCriteria? LastCriteria { get; private set; }

    public Dictionary<string, ProductDetails> ProductsBySlug { get; } = [];

    public Task<PagedResult<ProductSummary>> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken)
    {
        LastCriteria = criteria;
        return Task.FromResult(new PagedResult<ProductSummary>([], criteria.Page, criteria.PageSize, 0));
    }

    public Task<ProductDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(ProductsBySlug.GetValueOrDefault(slug));
}
