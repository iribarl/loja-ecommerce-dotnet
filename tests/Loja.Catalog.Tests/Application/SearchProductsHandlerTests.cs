using Loja.Catalog.Application.Products.SearchProducts;

namespace Loja.Catalog.Tests.Application;

public class SearchProductsHandlerTests
{
    private readonly FakeProductQueries _queries = new();
    private readonly SearchProductsHandler _handler;

    public SearchProductsHandlerTests() => _handler = new SearchProductsHandler(_queries);

    [Fact]
    public async Task MinPriceGreaterThanMax_Fails_WithoutQuerying()
    {
        var result = await Search(new SearchProductsRequest(MinPrice: 100, MaxPrice: 50));

        result.Error.ShouldBe(SearchProductsHandler.InvalidPriceRange);
        _queries.LastCriteria.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 12, 1, 12)]
    [InlineData(-5, 12, 1, 12)]
    [InlineData(3, 0, 3, 1)]
    [InlineData(2, 1000, 2, SearchProductsHandler.MaxPageSize)]
    public async Task Paging_IsClampedToValidRange(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        await Search(new SearchProductsRequest(Page: page, PageSize: pageSize));

        _queries.LastCriteria!.Page.ShouldBe(expectedPage);
        _queries.LastCriteria.PageSize.ShouldBe(expectedPageSize);
    }

    [Fact]
    public async Task BlankFilters_BecomeNull()
    {
        await Search(new SearchProductsRequest(Text: "   ", Category: " ", MinPrice: 0, MaxPrice: -1));

        var criteria = _queries.LastCriteria!;
        criteria.Text.ShouldBeNull();
        criteria.CategorySlug.ShouldBeNull();
        criteria.MinPrice.ShouldBeNull();
        criteria.MaxPrice.ShouldBeNull();
    }

    [Fact]
    public async Task Text_IsTrimmedAndTruncated()
    {
        await Search(new SearchProductsRequest(Text: "  " + new string('a', 150) + "  "));

        _queries.LastCriteria!.Text!.Length.ShouldBe(SearchProductsHandler.MaxTextLength);
    }

    [Fact]
    public async Task Attributes_WithBlankKeyOrValue_AreIgnored()
    {
        var attributes = new Dictionary<string, string> { [" Tamanho "] = " M ", ["Cor"] = "", [""] = "Azul" };

        await Search(new SearchProductsRequest(Attributes: attributes));

        _queries.LastCriteria!.Attributes.ShouldBe(new Dictionary<string, string> { ["Tamanho"] = "M" });
    }

    private Task<SharedKernel.Result<SharedKernel.PagedResult<Loja.Catalog.Application.Products.ProductSummary>>> Search(
        SearchProductsRequest request) =>
        _handler.HandleAsync(request, TestContext.Current.CancellationToken);
}
