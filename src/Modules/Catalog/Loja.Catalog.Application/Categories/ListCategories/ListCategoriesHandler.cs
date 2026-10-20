namespace Loja.Catalog.Application.Categories.ListCategories;

public sealed class ListCategoriesHandler(ICategoryQueries queries)
{
    public Task<IReadOnlyList<CategorySummary>> HandleAsync(CancellationToken cancellationToken) =>
        queries.ListAsync(cancellationToken);
}
