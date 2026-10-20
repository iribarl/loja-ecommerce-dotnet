using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.SearchProducts;

public sealed class SearchProductsHandler(IProductQueries queries)
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 48;
    public const int MaxTextLength = 100;

    public static readonly Error InvalidPriceRange =
        Error.Validation("Catalog.InvalidPriceRange", "O preço mínimo não pode ser maior que o máximo.");

    public async Task<Result<PagedResult<ProductSummary>>> HandleAsync(
        SearchProductsRequest request,
        CancellationToken cancellationToken)
    {
        // Validate after normalizing, so ignored values (e.g. a price of 0) cannot trigger errors.
        var criteria = Normalize(request);

        if (criteria.MinPrice is { } min && criteria.MaxPrice is { } max && min > max)
        {
            return InvalidPriceRange;
        }

        return await queries.SearchAsync(criteria, cancellationToken);
    }

    // Out-of-range paging or blank filters are corrected instead of rejected:
    // they usually come from hand-edited URLs and a sensible page is a better answer than an error.
    private static ProductSearchCriteria Normalize(SearchProductsRequest request)
    {
        var text = request.Text?.Trim();
        if (text?.Length > MaxTextLength)
        {
            text = text[..MaxTextLength];
        }

        var attributes = (request.Attributes ?? new Dictionary<string, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.Ordinal);

        return new ProductSearchCriteria(
            Text: string.IsNullOrEmpty(text) ? null : text,
            CategorySlug: string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            MinPrice: request.MinPrice is > 0 ? request.MinPrice : null,
            MaxPrice: request.MaxPrice is > 0 ? request.MaxPrice : null,
            Attributes: attributes,
            InStockOnly: request.InStockOnly,
            Page: Math.Max(request.Page, 1),
            PageSize: Math.Clamp(request.PageSize, 1, MaxPageSize));
    }
}
