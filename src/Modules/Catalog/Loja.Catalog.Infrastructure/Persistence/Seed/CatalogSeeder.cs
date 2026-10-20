using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Persistence.Seed;

/// <summary>
/// Fills an empty catalog with the demo data. Builds everything through the domain,
/// so the demo data obeys the same rules as real data.
/// </summary>
internal static class CatalogSeeder
{
    public const string ColorAttribute = "Cor";
    public const string SizeAttribute = "Tamanho";

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = CatalogSeedData.Categories
            .Select(name => Unwrap(Category.Create(name)))
            .ToDictionary(category => category.Name);

        db.Categories.AddRange(categories.Values);

        for (var i = 0; i < CatalogSeedData.Products.Length; i++)
        {
            db.Products.Add(CreateProduct(CatalogSeedData.Products[i], categories[CatalogSeedData.Products[i].Category].Id, i));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Product CreateProduct(ProductSeed seed, CategoryId categoryId, int productIndex)
    {
        var product = Unwrap(Product.Create(seed.Name, seed.Description, categoryId));
        Unwrap(product.AddImage(new Uri($"/images/products/{product.Slug}.svg", UriKind.Relative)));

        // A single-size product gets one variant per color, without a size attribute.
        string?[] sizes = seed.Sizes.Length > 0 ? [.. seed.Sizes] : [null];
        var variantIndex = 0;

        foreach (var color in seed.Colors)
        {
            foreach (var size in sizes)
            {
                var attributes = new Dictionary<string, string> { [ColorAttribute] = color };
                var sku = $"{seed.Code}-{Unwrap(Slug.From(color))}";

                if (size is not null)
                {
                    attributes[SizeAttribute] = size;
                    sku += $"-{size}";
                }

                Unwrap(product.AddVariant(
                    Unwrap(Sku.Create(sku)),
                    Unwrap(VariantAttributes.Create(attributes)),
                    Money.Of(seed.Price),
                    stock: DemoStock(productIndex, variantIndex++)));
            }
        }

        if (seed.Published)
        {
            Unwrap(product.Publish());
        }

        return product;
    }

    // Varied but repeatable stock levels, with some variants sold out.
    private static int DemoStock(int productIndex, int variantIndex) => (productIndex * 7 + variantIndex * 5) % 13;

    // The seed data is fixed: a failure here is a bug in the seed, not an expected outcome.
    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"Invalid seed data: {result.Error.Code}");

    private static void Unwrap(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid seed data: {result.Error.Code}");
        }
    }
}
