namespace Loja.Catalog.Application.Products.GetProductBySlug;

/// <param name="AttributeOptions">Choices for the variant selector, e.g. Tamanho: [P, M, G].</param>
public sealed record ProductPageResponse(
    ProductDetails Product,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AttributeOptions)
{
    /// <summary>
    /// The image to show for the shopper's current choices: the most specific image whose attributes
    /// all match the selection (e.g. {"Cor": "Preto"} when black is selected), otherwise the first
    /// general image, otherwise the first image. Null when the product has no images.
    /// </summary>
    public ProductImageDetails? ImageFor(IReadOnlyDictionary<string, string> selection) =>
        Product.Images
            .Where(image => image.AppliesTo.Count > 0
                && image.AppliesTo.All(pair => selection.TryGetValue(pair.Key, out var value) && value == pair.Value))
            .OrderByDescending(image => image.AppliesTo.Count)
            .FirstOrDefault()
        ?? Product.Images.FirstOrDefault(image => image.AppliesTo.Count == 0)
        ?? (Product.Images.Count > 0 ? Product.Images[0] : null);
}
