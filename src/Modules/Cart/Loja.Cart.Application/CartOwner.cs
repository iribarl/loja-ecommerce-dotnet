using Loja.Cart.Domain;

namespace Loja.Cart.Application;

/// <summary>
/// Who is shopping: every visitor has an anonymous cart id (from the cookie); signed-in
/// customers also have their customer id, and then their own cart is the one that counts.
/// </summary>
public sealed record CartOwner(Guid AnonymousCartId, Guid? CustomerId = null)
{
    internal async Task<ShoppingCart?> FindCartAsync(ICartRepository carts, CancellationToken cancellationToken)
    {
        if (CustomerId is { } customerId)
        {
            return await carts.GetByCustomerAsync(customerId, cancellationToken);
        }

        // A visitor never sees a cart that belongs to a customer, even if the cookie points to it
        // (e.g. on a shared computer after someone signed out).
        var cart = await carts.GetByIdAsync(new CartId(AnonymousCartId), cancellationToken);
        return cart is { CustomerId: null } ? cart : null;
    }

    internal ShoppingCart CreateCart(DateTimeOffset now) =>
        CustomerId is { } customerId
            ? ShoppingCart.CreateForCustomer(customerId, now)
            : ShoppingCart.CreateAnonymous(new CartId(AnonymousCartId), now);
}
