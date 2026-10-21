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
    internal ProductVariant(ProductVariantId id, int position, Sku sku, VariantAttributes attributes, Money price, int stock)
        : base(id)
    {
        Position = position;
        Sku = sku;
        Attributes = attributes;
        Price = price;
        Stock = stock;
    }

    // For EF Core materialization only: it cannot bind Money (a complex type) through the constructor.
    private ProductVariant()
        : base(default)
    {
        Sku = null!;
        Attributes = null!;
        Price = null!;
    }

    /// <summary>Display order within the product (0-based), e.g. sizes P, M, G, GG.</summary>
    public int Position { get; private set; }

    public Sku Sku { get; private set; }

    public VariantAttributes Attributes { get; private set; }

    public Money Price { get; private set; }

    public int Stock { get; private set; }

    public bool InStock => Stock > 0;

    internal void ChangePrice(Money price) => Price = price;

    internal void SetStock(int stock) => Stock = stock;

    internal void ChangeAttributes(VariantAttributes attributes) => Attributes = attributes;
}
