using Loja.Catalog.Application.Categories;
using Loja.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Queries;

internal sealed class CategoryQueries(CatalogDbContext db) : ICategoryQueries
{
    public async Task<IReadOnlyList<CategorySummary>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(c => new CategorySummary(c.Id.Value, c.Name, c.Slug.Value)).ToList();
    }
}
