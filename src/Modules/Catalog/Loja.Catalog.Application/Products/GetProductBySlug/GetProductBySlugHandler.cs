using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.GetProductBySlug;

public sealed class GetProductBySlugHandler(IProductQueries queries)
{
    public static readonly Error ProductNotFound =
        Error.NotFound("Catalog.ProductNotFound", "Produto não encontrado.");

    public async Task<Result<ProductPageResponse>> HandleAsync(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ProductNotFound;
        }

        var product = await queries.GetBySlugAsync(slug.Trim(), cancellationToken);

        return product is null
            ? ProductNotFound
            : new ProductPageResponse(product, AttributeOptions(product.Variants));
    }

    // The options a shopper can pick on the product page, e.g. Tamanho: [P, M, G], Cor: [Azul, Preto].
    // Values keep the order of the variants, so the catalog decides how sizes are listed.
    private static Dictionary<string, IReadOnlyList<string>> AttributeOptions(IReadOnlyList<VariantDetails> variants) =>
        variants
            .SelectMany(variant => variant.Attributes)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<string> (group) => group.Select(pair => pair.Value).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
}
