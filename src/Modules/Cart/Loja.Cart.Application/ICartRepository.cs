using Loja.Cart.Domain;

namespace Loja.Cart.Application;

public interface ICartRepository
{
    Task<ShoppingCart?> GetByIdAsync(CartId id, CancellationToken cancellationToken);

    Task<ShoppingCart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(ShoppingCart cart);

    void Remove(ShoppingCart cart);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
