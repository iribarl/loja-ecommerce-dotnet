using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Products;

public readonly record struct ProductVariantId(Guid Value)
{
    public static ProductVariantId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// A sellable version of a product (e.g. size M, blue). Part of the <see cref="Product"/> aggregate:
/// it is only changed through the product, which enforces the rules across variants.
/// </summary>
public sealed class ProductVariant : Entity<ProductVariantId>
{
    internal ProductVariant(ProductVariantId id, Sku sku, VariantAttributes attributes, Money price, int stock)
        : base(id)
    {
        Sku = sku;
        Attributes = attributes;
        Price = price;
        Stock = stock;
    }

    public Sku Sku { get; private set; }

    public VariantAttributes Attributes { get; private set; }

    public Money Price { get; private set; }

    public int Stock { get; private set; }

    public bool InStock => Stock > 0;

    internal void ChangePrice(Money price) => Price = price;

    internal void SetStock(int stock) => Stock = stock;
}
