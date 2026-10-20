using Loja.Catalog.Contracts;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Contracts;

/// <summary>
/// Stock reservations with atomic SQL updates.
/// </summary>
/// <remarks>
/// A deliberate exception to "change data through the aggregate": loading the product, checking
/// the stock in C# and saving it leaves a gap in which another customer can take the same unit.
/// A conditional UPDATE (… SET Stock = Stock - n WHERE Stock >= n) lets the database decide
/// atomically. The domain rule (stock never negative) is kept: it is the WHERE condition.
/// </remarks>
internal sealed class StockReservations(CatalogDbContext db, TimeProvider clock) : IStockReservations
{
    public async Task<ReservationResult> ReserveAsync(
        Guid orderId,
        IReadOnlyCollection<StockRequest> items,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        // A retry for an order that already has its reservations: nothing to do.
        if (await db.StockReservations.AnyAsync(r => r.OrderId == orderId, cancellationToken))
        {
            return ReservationResult.Success;
        }

        var now = clock.GetUtcNow();

        // Same variant twice in one request counts once, with the sum. Sorted by id: two orders that
        // lock the same rows always lock them in the same order, so they cannot deadlock.
        var requests = items
            .GroupBy(item => item.VariantId)
            .Select(group => new StockRequest(group.Key, group.Sum(item => item.Quantity)))
            .OrderBy(item => item.VariantId)
            .ToList();

        // The app uses Npgsql's retrying execution strategy, which does not allow a hand-made
        // transaction on its own: the whole block runs inside the strategy so it can be retried as one unit.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear(); // a retry starts clean
            var unavailable = new List<Guid>();

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var request in requests)
            {
                var variantId = new ProductVariantId(request.VariantId);
                var quantity = request.Quantity;

                var updated = await db.Set<ProductVariant>()
                    .Where(v => v.Id == variantId
                        && v.Stock >= quantity
                        && db.Products.Any(p => p.IsPublished && p.Variants.Any(pv => pv.Id == variantId)))
                    .ExecuteUpdateAsync(set => set.SetProperty(v => v.Stock, v => v.Stock - quantity), cancellationToken);

                if (updated == 0)
                {
                    unavailable.Add(request.VariantId);
                }
            }

            if (unavailable.Count > 0)
            {
                // All or nothing: undo the units already taken for the other items.
                await transaction.RollbackAsync(cancellationToken);
                return new ReservationResult(false, unavailable);
            }

            db.StockReservations.AddRange(requests.Select(request =>
                new StockReservation(orderId, new ProductVariantId(request.VariantId), request.Quantity, now, expiresAt)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ReservationResult.Success;
        });
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var reservations = await db.StockReservations
            .AsNoTracking()
            .Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Reserved)
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            await ReleaseOneAsync(reservation, cancellationToken);
        }
    }

    public Task ConfirmAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.StockReservations
            .Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Reserved)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.Status, ReservationStatus.Confirmed)
                    .SetProperty(r => r.ResolvedAt, clock.GetUtcNow()),
                cancellationToken);

    /// <summary>Returns to stock the units of reservations that stayed open past their expiry.</summary>
    /// <returns>How many reservations were released.</returns>
    public async Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = await db.StockReservations
            .AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Reserved && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var reservation in expired)
        {
            released += await ReleaseOneAsync(reservation, cancellationToken) ? 1 : 0;
        }

        return released;
    }

    // Two releases of the same reservation at the same time (e.g. the event delivered twice):
    // the status update only matches while the row is still Reserved, and Postgres makes the
    // second transaction wait and re-check it, so the stock is returned exactly once.
    private Task<bool> ReleaseOneAsync(StockReservation reservation, CancellationToken cancellationToken) =>
        // Inside the execution strategy, like ReserveAsync: retried as one unit if the connection drops.
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var claimed = await db.StockReservations
                .Where(r => r.Id == reservation.Id && r.Status == ReservationStatus.Reserved)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(r => r.Status, ReservationStatus.Released)
                        .SetProperty(r => r.ResolvedAt, clock.GetUtcNow()),
                    cancellationToken);

            if (claimed == 1)
            {
                var quantity = reservation.Quantity;
                await db.Set<ProductVariant>()
                    .Where(v => v.Id == reservation.VariantId)
                    .ExecuteUpdateAsync(set => set.SetProperty(v => v.Stock, v => v.Stock + quantity), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return claimed == 1;
        });
}
