using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Stock;

/// <summary>
/// A manual change to a variant's stock made in the admin panel ("+10 reposição", "-2 avaria").
/// Kept as history, so every unit that was not sold or reserved can be explained.
/// </summary>
/// <remarks>
/// Applied as a relative change (Stock + Quantity) by one atomic UPDATE, never as "the stock is now
/// N": an order placed while the admin types must not be overwritten.
/// </remarks>
public sealed class StockAdjustment
{
    public const int MaxReasonLength = 200;

    /// <summary>Guards against typos such as an extra zero.</summary>
    public const int MaxQuantity = 100_000;

    private StockAdjustment(
        ProductVariantId variantId, int quantity, string reason, int stockAfter, Guid adjustedBy, DateTimeOffset adjustedAt)
    {
        Id = Guid.CreateVersion7();
        VariantId = variantId;
        Quantity = quantity;
        Reason = reason;
        StockAfter = stockAfter;
        AdjustedBy = adjustedBy;
        AdjustedAt = adjustedAt;
    }

    public Guid Id { get; private set; }

    public ProductVariantId VariantId { get; private set; }

    /// <summary>Units added (positive) or removed (negative).</summary>
    public int Quantity { get; private set; }

    public string Reason { get; private set; }

    public int StockAfter { get; private set; }

    /// <summary>The admin's customer id (Customers module); the Catalog knows it only as an id.</summary>
    public Guid AdjustedBy { get; private set; }

    public DateTimeOffset AdjustedAt { get; private set; }

    /// <summary>The rules for an adjustment, checked before touching the stock.</summary>
    public static Result Validate(int quantity, string? reason)
    {
        if (quantity == 0)
        {
            return CatalogErrors.ZeroAdjustment;
        }

        if (Math.Abs(quantity) > MaxQuantity)
        {
            return CatalogErrors.AdjustmentTooLarge;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > MaxReasonLength)
        {
            return CatalogErrors.AdjustmentReasonRequired;
        }

        return Result.Success();
    }

    /// <param name="stockAfter">The variant's stock once the change was applied.</param>
    public static Result<StockAdjustment> Create(
        ProductVariantId variantId, int quantity, string reason, int stockAfter, Guid adjustedBy, DateTimeOffset adjustedAt)
    {
        var valid = Validate(quantity, reason);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        if (stockAfter < 0)
        {
            return CatalogErrors.NegativeStock;
        }

        return new StockAdjustment(variantId, quantity, reason.Trim(), stockAfter, adjustedBy, adjustedAt);
    }
}
