using Loja.Orders.Application;
using Loja.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loja.Orders.Infrastructure.Persistence;

internal sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        await db.Orders
            .AsNoTracking()
            .Where(o => o.Customer.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .ToListAsync(cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
