using Loja.Catalog.Application.Products;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Queries;

internal sealed class ProductAdminQueries(CatalogDbContext db) : IProductAdminQueries
{
    public async Task<PagedResult<AdminProductRow>> ListAsync(AdminProductFilter filter, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking();

        if (filter.Text is { } text)
        {
            // The panel searches by name or SKU, as staff look things up; ILIKE is fine for this size.
            // The SKU is a value object behind a converter, so its condition is written in SQL
            // (the pattern travels as a parameter).
            var pattern = $"%{EscapeLike(text)}%";
            var bySku = db.Set<ProductVariant>().FromSql(
                $"""SELECT * FROM catalog.product_variants WHERE "Sku" ILIKE {pattern}""");
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern)
                || bySku.Any(v => EF.Property<ProductId>(v, "ProductId") == p.Id));
        }

        if (filter.Published is { } published)
        {
            products = products.Where(p => p.IsPublished == published);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var rows = await products
            .OrderBy(p => p.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                CategoryName = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First(),
                p.IsPublished,
                VariantCount = p.Variants.Count,
                TotalStock = p.Variants.Sum(v => v.Stock),
                MinPrice = p.Variants.Min(v => (decimal?)v.Price.Amount),
                Currency = p.Variants.Select(v => v.Price.Currency).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new AdminProductRow(
                r.Id.Value, r.Name, r.Slug.Value, r.CategoryName, r.IsPublished, r.VariantCount, r.TotalStock, r.MinPrice,
                r.Currency ?? Money.DefaultCurrency))
            .ToList();

        return new PagedResult<AdminProductRow>(items, filter.Page, filter.PageSize, totalCount);
    }

    public async Task<AdminProduct?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var productId = new ProductId(id);
        var product = await db.Products.AsNoTracking().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var variants = product.Variants
            .OrderBy(v => v.Position)
            .Select(v => new AdminVariant(v.Id.Value, v.Sku.Value, v.Attributes.Values, v.Price.Amount, v.Price.Currency, v.Stock))
            .ToList();

        return new AdminProduct(
            product.Id.Value, product.Name, product.Slug.Value, product.Description, product.CategoryId.Value, product.IsPublished, variants);
    }

    public async Task<IReadOnlyList<AdminStockAdjustment>> ListStockAdjustmentsAsync(
        Guid variantId, int count, CancellationToken cancellationToken)
    {
        var id = new ProductVariantId(variantId);
        return await db.StockAdjustments.AsNoTracking()
            .Where(a => a.VariantId == id)
            .OrderByDescending(a => a.AdjustedAt)
            .Take(count)
            .Select(a => new AdminStockAdjustment(a.Quantity, a.Reason, a.StockAfter, a.AdjustedAt))
            .ToListAsync(cancellationToken);
    }

    // % and _ typed by the admin are searched literally, not as wildcards.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
