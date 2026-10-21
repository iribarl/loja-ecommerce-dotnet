using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.ManageProducts;

public sealed class ListAdminProductsHandler(IProductAdminQueries queries)
{
    public const int PageSize = 20;

    public Task<PagedResult<AdminProductRow>> HandleAsync(string? text, bool? published, int page, CancellationToken cancellationToken) =>
        queries.ListAsync(
            new AdminProductFilter(string.IsNullOrWhiteSpace(text) ? null : text.Trim(), published, Math.Max(page, 1), PageSize),
            cancellationToken);
}

public sealed class GetAdminProductHandler(IProductAdminQueries queries)
{
    public Task<AdminProduct?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        queries.GetAsync(id, cancellationToken);
}
