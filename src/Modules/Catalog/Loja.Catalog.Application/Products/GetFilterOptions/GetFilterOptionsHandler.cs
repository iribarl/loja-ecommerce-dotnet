using Loja.Catalog.Application.Categories;

namespace Loja.Catalog.Application.Products.GetFilterOptions;

/// <summary>
/// Everything the search page needs to draw its filters.
/// </summary>
public sealed class GetFilterOptionsHandler(ICategoryQueries categories, IProductQueries products)
{
    public async Task<FilterOptionsResponse> HandleAsync(string? categorySlug, CancellationToken cancellationToken)
    {
        var category = string.IsNullOrWhiteSpace(categorySlug) ? null : categorySlug.Trim();

        var categoryList = await categories.ListAsync(cancellationToken);
        var attributes = await products.GetAttributeOptionsAsync(category, cancellationToken);

        return new FilterOptionsResponse(categoryList, attributes);
    }
}

public sealed record FilterOptionsResponse(
    IReadOnlyList<CategorySummary> Categories,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes);
