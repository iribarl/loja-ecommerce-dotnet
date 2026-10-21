using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>The customer's orders, newest first.</summary>
    Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Every customer's orders, newest first, for the admin panel.</summary>
    Task<PagedResult<Order>> SearchAsync(OrderSearch search, CancellationToken cancellationToken);

    Task<int> CountAsync(OrderStatus status, CancellationToken cancellationToken);

    void Add(Order order);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves unless the order changed since it was loaded (e.g. the payment webhook meanwhile).
    /// </summary>
    /// <returns>False when someone else changed it first; nothing is saved.</returns>
    Task<bool> SaveIfUnchangedAsync(CancellationToken cancellationToken);
}

/// <param name="Code">The order's short code (e.g. "3F9A1C2B") or its end.</param>
public sealed record OrderSearch(OrderStatus? Status, string? Code, int Page, int PageSize);
