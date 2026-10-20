using Loja.Cart.Domain;
using Loja.Catalog.Contracts;
using Loja.SharedKernel;

namespace Loja.Cart.Application.AddToCart;

public sealed class AddToCartHandler(ICartRepository carts, ICatalogApi catalog, TimeProvider clock)
{
    /// <returns>The total units in the cart after adding, for the header badge.</returns>
    public async Task<Result<int>> HandleAsync(
        CartOwner owner,
        Guid variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return CartErrors.InvalidQuantity;
        }

        var variant = await catalog.GetVariantAsync(variantId, cancellationToken);
        if (variant is not { CanBePurchased: true })
        {
            return CartErrors.ProductUnavailable;
        }

        var now = clock.GetUtcNow();
        var cart = await owner.FindCartAsync(carts, cancellationToken);
        var isNew = cart is null;
        cart ??= owner.CreateCart(now);

        // Units already in the cart count too: 2 in the cart + 2 more needs 4 in stock.
        if (cart.QuantityOf(variantId) + quantity > variant.AvailableStock)
        {
            return CartErrors.NotEnoughStock;
        }

        var added = cart.AddItem(variantId, quantity, now);
        if (added.IsFailure)
        {
            return added.Error;
        }

        if (isNew)
        {
            carts.Add(cart);
        }

        await carts.SaveChangesAsync(cancellationToken);
        return cart.TotalUnits;
    }
}
