using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.ManageProducts;

/// <summary>What the admin types for a product.</summary>
public sealed record ProductInput(string Name, string Description, Guid CategoryId);

public sealed class CreateProductHandler(IProductRepository products, ICatalogCacheInvalidator cache)
{
    /// <summary>"camiseta", "camiseta-2", ... "camiseta-20"; after that the admin should pick another name.</summary>
    public const int MaxSlugAttempts = 20;

    public static readonly Error NameTaken =
        Error.Conflict("Catalog.NameTaken", "Já existem muitos produtos com este nome. Escolha outro.");

    /// <returns>The new product's id. It starts as a draft, hidden from the store.</returns>
    public async Task<Result<Guid>> HandleAsync(ProductInput input, CancellationToken cancellationToken)
    {
        var categoryId = new CategoryId(input.CategoryId);
        if (!await products.CategoryExistsAsync(categoryId, cancellationToken))
        {
            return CatalogErrors.CategoryNotFound;
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return CatalogErrors.NameRequired;
        }

        var baseSlug = Slug.From(input.Name);
        if (baseSlug.IsFailure)
        {
            return baseSlug.Error;
        }

        var slug = await FreeSlugAsync(baseSlug.Value, cancellationToken);
        if (slug is null)
        {
            return NameTaken;
        }

        var product = Product.Create(input.Name, input.Description, categoryId, slug);
        if (product.IsFailure)
        {
            return product.Error;
        }

        products.Add(product.Value);
        await products.SaveChangesAsync(cancellationToken);

        // A draft does not show in the store, but the cache is dropped anyway: cheap and never stale.
        await cache.InvalidateAsync(cancellationToken);
        return product.Value.Id.Value;
    }

    private async Task<Slug?> FreeSlugAsync(Slug slug, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSlugAttempts; attempt++)
        {
            var candidate = attempt == 1 ? slug : Slug.From($"{slug.Value}-{attempt}").Value;
            if (!await products.SlugExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}

public sealed class UpdateProductHandler(IProductRepository products, ICatalogCacheInvalidator cache)
{
    public async Task<Result> HandleAsync(Guid productId, ProductInput input, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        var categoryId = new CategoryId(input.CategoryId);
        if (!await products.CategoryExistsAsync(categoryId, cancellationToken))
        {
            return CatalogErrors.CategoryNotFound;
        }

        var result = product.UpdateDetails(input.Name, input.Description, categoryId);
        if (result.IsFailure)
        {
            return result;
        }

        await products.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Shows the product in the store, or hides it again.</summary>
public sealed class PublishProductHandler(IProductRepository products, ICatalogCacheInvalidator cache)
{
    public async Task<Result> HandleAsync(Guid productId, bool publish, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        if (publish)
        {
            var result = product.Publish();
            if (result.IsFailure)
            {
                return result;
            }
        }
        else
        {
            product.Unpublish();
        }

        await products.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}
