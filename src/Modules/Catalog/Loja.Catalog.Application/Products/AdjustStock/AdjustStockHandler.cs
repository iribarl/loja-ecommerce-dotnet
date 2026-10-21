using Loja.Catalog.Domain.Stock;
using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.AdjustStock;

/// <summary>Applies a stock change atomically and records it in the history.</summary>
public interface IStockAdjustments
{
    /// <returns>The variant's stock after the change.</returns>
    Task<Result<int>> AdjustAsync(Guid variantId, int quantity, string reason, Guid adjustedBy, CancellationToken cancellationToken);
}

public sealed class AdjustStockHandler(IStockAdjustments adjustments, ICatalogCacheInvalidator cache)
{
    /// <param name="quantity">Units in (positive) or out (negative).</param>
    /// <param name="adjustedBy">The admin making the change.</param>
    /// <returns>The variant's stock after the change.</returns>
    public async Task<Result<int>> HandleAsync(
        Guid variantId, int quantity, string? reason, Guid adjustedBy, CancellationToken cancellationToken)
    {
        // The rules are checked first, so a wrong form never reaches the database.
        var valid = StockAdjustment.Validate(quantity, reason);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var result = await adjustments.AdjustAsync(variantId, quantity, reason!, adjustedBy, cancellationToken);
        if (result.IsSuccess)
        {
            // The store shows "Esgotado" / "Últimas unidades" from the cache.
            await cache.InvalidateAsync(cancellationToken);
        }

        return result;
    }
}
