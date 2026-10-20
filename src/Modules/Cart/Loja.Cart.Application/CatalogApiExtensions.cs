using Loja.Catalog.Contracts;

namespace Loja.Cart.Application;

internal static class CatalogApiExtensions
{
    /// <summary>One variant, or null when the catalog does not know it.</summary>
    public static async Task<VariantSnapshot?> GetVariantAsync(
        this ICatalogApi catalog,
        Guid variantId,
        CancellationToken cancellationToken)
    {
        var variants = await catalog.GetVariantsAsync([variantId], cancellationToken);
        return variants.Count > 0 ? variants[0] : null;
    }
}
