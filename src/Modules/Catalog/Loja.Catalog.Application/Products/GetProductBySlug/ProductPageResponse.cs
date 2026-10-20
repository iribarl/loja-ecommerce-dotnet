namespace Loja.Catalog.Application.Products.GetProductBySlug;

/// <param name="AttributeOptions">Choices for the variant selector, e.g. Tamanho: [P, M, G].</param>
public sealed record ProductPageResponse(
    ProductDetails Product,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AttributeOptions);
