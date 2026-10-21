using Loja.Orders.Application;
using Loja.Orders.Domain;
using Loja.SharedKernel;
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

    public async Task<PagedResult<Order>> SearchAsync(OrderSearch search, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        if (search.Code is { } code)
        {
            // The short code is the end of the id without hyphens ("…3f9a1c2b"). The id has a
            // converter, so the match is written in SQL (the pattern travels as a parameter) and
            // returns only ids: composing FromSql over this entity's nested complex types makes EF
            // name their columns wrongly. Few orders match a code, so two queries are cheap.
            var pattern = "%" + EscapeLike(code).ToLowerInvariant();
            var ids = (await db.Database
                    .SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM orders.orders WHERE replace("Id"::text, '-', '') LIKE {pattern} LIMIT 100""")
                    .ToListAsync(cancellationToken))
                .Select(id => new OrderId(id))
                .ToList();

            orders = orders.Where(o => ids.Contains(o.Id));
        }
        if (search.Status is { } status)
        {
            orders = orders.Where(o => o.Status == status);
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(o => o.PlacedAt)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Order>(items, search.Page, search.PageSize, totalCount);
    }

    public Task<int> CountAsync(OrderStatus status, CancellationToken cancellationToken) =>
        db.Orders.CountAsync(o => o.Status == status, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveIfUnchangedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // xmin changed: someone else saved this order after we read it.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    // % and _ typed by the admin are searched literally, not as wildcards.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
