using Loja.Orders.Domain;

namespace Loja.Orders.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>The customer's orders, newest first.</summary>
    Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(Order order);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
