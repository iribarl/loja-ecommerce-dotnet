using Loja.Cart.Domain;
using Loja.Catalog.Contracts;
using Loja.SharedKernel;

namespace Loja.Cart.Application.UpdateCartItem;

/// <summary>
/// Changes the quantity of a line. Zero removes it.
/// </summary>
public sealed class UpdateCartItemHandler(ICartRepository carts, ICatalogApi catalog, TimeProvider clock)
{
    public async Task<Result> HandleAsync(
        CartOwner owner,
        Guid variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var cart = await owner.FindCartAsync(carts, cancellationToken);
        if (cart is null)
        {
            return CartErrors.ItemNotFound;
        }

        // Removing never needs the catalog: a shopper must always be able to drop a product,
        // even one that no longer exists.
        if (quantity > 0)
        {
            var variant = await catalog.GetVariantAsync(variantId, cancellationToken);
            if (variant is not { CanBePurchased: true })
            {
                return CartErrors.ProductUnavailable;
            }

            if (quantity > variant.AvailableStock)
            {
                return CartErrors.NotEnoughStock;
            }
        }

        var result = cart.SetQuantity(variantId, quantity, clock.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        await carts.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
