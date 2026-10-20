using Loja.Cart.Application;
using Loja.Cart.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loja.Cart.Infrastructure.Persistence;

internal sealed class CartRepository(CartDbContext db) : ICartRepository
{
    // Items are owned by the cart, so EF loads them with it automatically.
    public Task<ShoppingCart?> GetByIdAsync(CartId id, CancellationToken cancellationToken) =>
        db.Carts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<ShoppingCart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Carts.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public void Add(ShoppingCart cart) => db.Carts.Add(cart);

    public void Remove(ShoppingCart cart) => db.Carts.Remove(cart);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
