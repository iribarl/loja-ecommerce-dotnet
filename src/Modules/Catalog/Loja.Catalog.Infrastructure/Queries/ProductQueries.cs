using System.Text.Json;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Configurations;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Loja.Catalog.Infrastructure.Queries;

internal sealed class ProductQueries(CatalogDbContext db) : IProductQueries
{
    private const string TextSearchConfig = "portuguese";

    public async Task<PagedResult<ProductSummary>> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking().Where(p => p.IsPublished);

        if (criteria.Text is { } text)
        {
            products = products.Where(p => EF.Property<NpgsqlTsVector>(p, ProductConfiguration.SearchVector)
                .Matches(EF.Functions.WebSearchToTsQuery(TextSearchConfig, text)));
        }

        if (criteria.CategorySlug is not null)
        {
            if (Slug.From(criteria.CategorySlug) is not { IsSuccess: true } slug)
            {
                return Empty(criteria);
            }

            var categorySlug = slug.Value;
            products = products.Where(p => db.Categories.Any(c => c.Id == p.CategoryId && c.Slug == categorySlug));
        }

        products = FilterByVariants(db, products, criteria);

        var totalCount = await products.CountAsync(cancellationToken);

        var ordered = criteria.Text is { } rankText
            ? products
                .OrderByDescending(p => EF.Property<NpgsqlTsVector>(p, ProductConfiguration.SearchVector)
                    .Rank(EF.Functions.WebSearchToTsQuery(TextSearchConfig, rankText)))
                .ThenBy(p => p.Name)
            : products.OrderBy(p => p.Name);

        var rows = await ordered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                CategoryName = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First(),
                MinPrice = p.Variants.Min(v => v.Price.Amount),
                Currency = p.Variants.OrderBy(v => v.Price.Amount).Select(v => v.Price.Currency).First(),
                p.Images,
                InStock = p.Variants.Any(v => v.Stock > 0),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new ProductSummary(
                r.Id.Value, r.Name, r.Slug.Value, r.CategoryName, r.MinPrice, r.Currency, r.Images.Count > 0 ? r.Images[0] : null, r.InStock))
            .ToList();

        return new PagedResult<ProductSummary>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<ProductDetails?> GetBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        if (Slug.From(slug) is not { IsSuccess: true } validSlug)
        {
            return null;
        }

        var productSlug = validSlug.Value;
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.IsPublished && p.Slug == productSlug, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var category = await db.Categories
            .AsNoTracking()
            .FirstAsync(c => c.Id == product.CategoryId, cancellationToken);

        var variants = product.Variants
            .OrderBy(v => v.Position)
            .Select(v => new VariantDetails(
                v.Id.Value, v.Sku.Value, v.Attributes.Values, v.Price.Amount, v.Price.Currency, v.InStock))
            .ToList();

        return new ProductDetails(
            product.Id.Value,
            product.Name,
            product.Slug.Value,
            product.Description,
            category.Name,
            category.Slug.Value,
            product.Images,
            variants);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAttributeOptionsAsync(
        string? categorySlug,
        CancellationToken cancellationToken)
    {
        string? slug = null;
        if (categorySlug is not null)
        {
            if (Slug.From(categorySlug) is not { IsSuccess: true } validSlug)
            {
                return new Dictionary<string, IReadOnlyList<string>>();
            }

            slug = validSlug.Value.Value;
        }

        // jsonb_each_text turns {"Tamanho": "M", "Cor": "Azul"} into rows (Tamanho, M), (Cor, Azul).
        // Values are ordered by their lowest variant position, so sizes come out as P, M, G, GG.
        var rows = await db.Database
            .SqlQuery<AttributeValueRow>($"""
                SELECT a.key AS "Key", a.value AS "Value", MIN(v."Position") AS "Position"
                FROM catalog.product_variants v
                JOIN catalog.products p ON p."Id" = v."ProductId"
                JOIN catalog.categories c ON c."Id" = p."CategoryId"
                CROSS JOIN LATERAL jsonb_each_text(v."Attributes") a
                WHERE p."IsPublished" AND ({slug}::text IS NULL OR c."Slug" = {slug})
                GROUP BY a.key, a.value
                """)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<string> (g) => g.OrderBy(r => r.Position).ThenBy(r => r.Value, StringComparer.Ordinal)
                    .Select(r => r.Value)
                    .ToList(),
                StringComparer.Ordinal);
    }

    private sealed record AttributeValueRow(string Key, string Value, int Position);

    // A product matches when at least one of its variants matches every variant filter at once
    // (e.g. size M *and* in stock, not size M out of stock plus another size in stock).
    private static IQueryable<Product> FilterByVariants(
        CatalogDbContext db,
        IQueryable<Product> products,
        ProductSearchCriteria criteria)
    {
        if (criteria.MinPrice is null && criteria.MaxPrice is null && !criteria.InStockOnly && criteria.Attributes.Count == 0)
        {
            return products;
        }

        var min = criteria.MinPrice;
        var max = criteria.MaxPrice;
        var inStockOnly = criteria.InStockOnly;

        return products.Where(p => VariantsMatchingAttributes(db, criteria.Attributes).Any(v =>
            EF.Property<ProductId>(v, "ProductId") == p.Id
            && (min == null || v.Price.Amount >= min)
            && (max == null || v.Price.Amount <= max)
            && (!inStockOnly || v.Stock > 0)));
    }

    // EF does not pass a parameter of JsonContains through the column's value converter, so the
    // jsonb containment (@>) is written in SQL. The JSON travels as a parameter, never concatenated.
    // FromSql stays composable: EF wraps it as a subquery and adds the LINQ conditions around it.
    private static IQueryable<ProductVariant> VariantsMatchingAttributes(
        CatalogDbContext db,
        IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.Count == 0)
        {
            return db.Set<ProductVariant>();
        }

        var json = JsonSerializer.Serialize(attributes);
        return db.Set<ProductVariant>().FromSql(
            $"""SELECT * FROM catalog.product_variants WHERE "Attributes" @> {json}::jsonb""");
    }

    private static PagedResult<ProductSummary> Empty(ProductSearchCriteria criteria) =>
        new([], criteria.Page, criteria.PageSize, 0);
}
