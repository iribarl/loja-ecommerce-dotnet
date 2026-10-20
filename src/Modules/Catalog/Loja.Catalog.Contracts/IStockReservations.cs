namespace Loja.Catalog.Contracts;

/// <summary>
/// Holds stock for orders. Reserving takes the units out of the available stock at once,
/// so two customers can never buy the same last unit.
/// </summary>
public interface IStockReservations
{
    /// <summary>
    /// All or nothing: either every item is reserved or nothing changes. Calling it again for the
    /// same order (a retry) does not reserve twice.
    /// </summary>
    /// <param name="expiresAt">If the reservation is still open then, the units return to stock.</param>
    Task<ReservationResult> ReserveAsync(
        Guid orderId,
        IReadOnlyCollection<StockRequest> items,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    /// <summary>Returns the units of an unpaid order to stock. Idempotent.</summary>
    Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>The order was paid: the reserved units are sold. Idempotent.</summary>
    Task ConfirmAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed record StockRequest(Guid VariantId, int Quantity);

/// <param name="UnavailableVariantIds">Variants without enough stock (or no longer for sale).</param>
public sealed record ReservationResult(bool Succeeded, IReadOnlyList<Guid> UnavailableVariantIds)
{
    public static readonly ReservationResult Success = new(true, []);
}
