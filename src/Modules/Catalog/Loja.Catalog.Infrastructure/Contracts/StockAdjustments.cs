using Loja.Catalog.Application.Products.AdjustStock;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Contracts;

/// <summary>
/// Stock adjustments with an atomic SQL update, like <see cref="StockReservations"/>: the database
/// applies Stock + n only if the result is not negative, so a purchase at the same moment is
/// never overwritten nor oversold.
/// </summary>
internal sealed class StockAdjustments(CatalogDbContext db, TimeProvider clock) : IStockAdjustments
{
    public Task<Result<int>> AdjustAsync(
        Guid variantId, int quantity, string reason, Guid adjustedBy, CancellationToken cancellationToken)
    {
        var id = new ProductVariantId(variantId);

        // Inside the execution strategy: retried as one unit if the connection drops.
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var updated = await db.Set<ProductVariant>()
                .Where(v => v.Id == id && v.Stock + quantity >= 0)
                .ExecuteUpdateAsync(set => set.SetProperty(v => v.Stock, v => v.Stock + quantity), cancellationToken);

            // After the UPDATE this transaction holds the row lock: the stock read is the one just written.
            var stock = await db.Set<ProductVariant>().Where(v => v.Id == id).Select(v => (int?)v.Stock)
                .SingleOrDefaultAsync(cancellationToken);

            if (stock is null)
            {
                return Result.Failure<int>(CatalogErrors.VariantNotFound);
            }

            if (updated == 0)
            {
                return Result.Failure<int>(CatalogErrors.StockWouldBeNegative(stock.Value));
            }

            var adjustment = StockAdjustment.Create(id, quantity, reason, stock.Value, adjustedBy, clock.GetUtcNow());
            if (adjustment.IsFailure)
            {
                return Result.Failure<int>(adjustment.Error); // rolled back with the transaction
            }

            db.StockAdjustments.Add(adjustment.Value);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success(stock.Value);
        });
    }
}
