using Loja.Cart.Domain;

namespace Loja.Cart.Application;

/// <summary>
/// Who is shopping: every visitor has an anonymous cart id (from the cookie); signed-in
/// customers also have their customer id, and then their own cart is the one that counts.
/// </summary>
public sealed record CartOwner(Guid AnonymousCartId, Guid? CustomerId = null)
{
    internal Task<ShoppingCart?> FindCartAsync(ICartRepository carts, CancellationToken cancellationToken) =>
        CustomerId is { } customerId
            ? carts.GetByCustomerAsync(customerId, cancellationToken)
            : carts.GetByIdAsync(new CartId(AnonymousCartId), cancellationToken);

    internal ShoppingCart CreateCart(DateTimeOffset now) =>
        CustomerId is { } customerId
            ? ShoppingCart.CreateForCustomer(customerId, now)
            : ShoppingCart.CreateAnonymous(new CartId(AnonymousCartId), now);
}
