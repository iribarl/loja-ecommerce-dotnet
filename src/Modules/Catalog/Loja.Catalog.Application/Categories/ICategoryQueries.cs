namespace Loja.Catalog.Application.Categories;

/// <summary>
/// Read side of the categories.
/// </summary>
public interface ICategoryQueries
{
    /// <summary>Categories ordered by name.</summary>
    Task<IReadOnlyList<CategorySummary>> ListAsync(CancellationToken cancellationToken);
}

public sealed record CategorySummary(Guid Id, string Name, string Slug);
