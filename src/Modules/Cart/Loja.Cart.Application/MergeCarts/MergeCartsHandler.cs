using Loja.Cart.Domain;

namespace Loja.Cart.Application.MergeCarts;

/// <summary>
/// When a visitor signs in, what they put in the cart as anonymous joins their customer cart.
/// </summary>
/// <remarks>
/// Stock is not checked here: the cart page flags lines without enough stock, and the shopper decides.
/// Losing items silently at sign-in would be worse.
/// </remarks>
public sealed class MergeCartsHandler(ICartRepository carts, TimeProvider clock)
{
    public async Task HandleAsync(Guid anonymousCartId, Guid customerId, CancellationToken cancellationToken)
    {
        var anonymous = await carts.GetByIdAsync(new CartId(anonymousCartId), cancellationToken);

        // Nothing to merge, or the cart already belongs to someone (never take another customer's cart).
        if (anonymous is null || anonymous.CustomerId is not null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var customerCart = await carts.GetByCustomerAsync(customerId, cancellationToken);

        if (customerCart is null)
        {
            // First cart of this customer: the anonymous one simply becomes theirs.
            anonymous.AssignTo(customerId, now);
        }
        else
        {
            customerCart.Merge(anonymous, now);
            carts.Remove(anonymous);
        }

        // A conflict here means another tab changed the cart at the same moment; the items are
        // still in the anonymous cart, so nothing is lost and signing in must not fail because of it.
        await carts.SaveChangesAsync(cancellationToken);
    }
}
