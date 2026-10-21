using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.ManageProducts;

/// <param name="Attributes">One "Name: value" per line, e.g. "Tamanho: M". See <see cref="AttributesText"/>.</param>
public sealed record NewVariantInput(string Sku, string Attributes, decimal Price, int Stock);

/// <summary>The SKU cannot change: reservations and orders refer to the variant by it.</summary>
public sealed record VariantInput(string Attributes, decimal Price);

public sealed class AddVariantHandler(IProductRepository products, ICatalogCacheInvalidator cache)
{
    /// <returns>The new variant's id.</returns>
    public async Task<Result<Guid>> HandleAsync(Guid productId, NewVariantInput input, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        var sku = Sku.Create(input.Sku);
        if (sku.IsFailure)
        {
            return sku.Error;
        }

        var attributes = AttributesText.Parse(input.Attributes);
        if (attributes.IsFailure)
        {
            return attributes.Error;
        }

        // Within the product the aggregate checks it; across products only the database knows.
        if (product.Variants.All(v => v.Sku != sku.Value) && await products.SkuExistsAsync(sku.Value, cancellationToken))
        {
            return CatalogErrors.SkuInUse;
        }

        var variant = product.AddVariant(sku.Value, attributes.Value, Money.Of(input.Price), input.Stock);
        if (variant.IsFailure)
        {
            return variant.Error;
        }

        await products.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return variant.Value.Id.Value;
    }
}

public sealed class UpdateVariantHandler(IProductRepository products, ICatalogCacheInvalidator cache)
{
    public async Task<Result> HandleAsync(Guid productId, Guid variantId, VariantInput input, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        var variant = product?.Variants.FirstOrDefault(v => v.Id.Value == variantId);
        if (product is null || variant is null)
        {
            return CatalogErrors.VariantNotFound;
        }

        var attributes = AttributesText.Parse(input.Attributes);
        if (attributes.IsFailure)
        {
            return attributes.Error;
        }

        // Both changes or neither: nothing is saved until both rules passed.
        var result = product.ChangeAttributes(variant.Sku, attributes.Value);
        if (result.IsSuccess)
        {
            result = product.ChangePrice(variant.Sku, Money.Of(input.Price));
        }

        if (result.IsFailure)
        {
            return result;
        }

        await products.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Variant attributes as the admin types them: one "Name: value" per line ("Tamanho: M").
/// </summary>
public static class AttributesText
{
    public static Result<VariantAttributes> Parse(string? text)
    {
        var pairs = new List<KeyValuePair<string, string>>();

        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                return CatalogErrors.InvalidAttribute;
            }

            pairs.Add(KeyValuePair.Create(line[..colon], line[(colon + 1)..]));
        }

        return VariantAttributes.Create(pairs);
    }

    public static string Format(IReadOnlyDictionary<string, string> attributes) =>
        string.Join('\n', attributes.Select(pair => $"{pair.Key}: {pair.Value}"));
}
