using Loja.Cart.Application;
using Loja.Cart.Contracts;

namespace Loja.Cart.Infrastructure;

internal sealed class CartApi(ICartRepository carts, TimeProvider clock) : ICartApi
{
    public async Task<IReadOnlyList<CartLine>> GetCustomerCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await carts.GetByCustomerAsync(customerId, cancellationToken);

        return cart is null
            ? []
            : cart.Items.OrderBy(i => i.AddedAt).Select(i => new CartLine(i.VariantId, i.Quantity)).ToList();
    }

    public async Task ClearCustomerCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await carts.GetByCustomerAsync(customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return;
        }

        cart.Clear(clock.GetUtcNow());
        await carts.SaveChangesAsync(cancellationToken);
    }
}
