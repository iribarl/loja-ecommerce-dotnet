using Loja.Cart.Domain;

namespace Loja.Cart.Application;

public interface ICartRepository
{
    Task<ShoppingCart?> GetByIdAsync(CartId id, CancellationToken cancellationToken);

    Task<ShoppingCart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(ShoppingCart cart);

    void Remove(ShoppingCart cart);

    /// <returns>False when the cart was changed elsewhere since it was read (e.g. in another tab).</returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
