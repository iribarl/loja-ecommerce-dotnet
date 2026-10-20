namespace Loja.Catalog.Domain.Products;

/// <summary>
/// A product picture. It may apply to some variants only, e.g. AppliesTo {"Cor": "Preto"}
/// for the black version; with no attributes it is a general image of the product.
/// </summary>
public sealed record ProductImage(Uri Url, VariantAttributes AppliesTo)
{
    public bool IsGeneral => AppliesTo.Values.Count == 0;
}
