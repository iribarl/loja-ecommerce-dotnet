using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Domain.Stock;

public enum ReservationStatus
{
    /// <summary>Stock taken for an order that is not paid yet.</summary>
    Reserved,

    /// <summary>The order was paid: the units are sold for good.</summary>
    Confirmed,

    /// <summary>The order was cancelled or the reservation expired: the units went back to stock.</summary>
    Released,
}

/// <summary>
/// Units of one variant held for one order. Reserving already takes the units out of
/// <see cref="ProductVariant.Stock"/>; releasing puts them back; confirming makes it final.
/// </summary>
/// <remarks>
/// Kept per order so that releasing or confirming twice (an event delivered twice) has no effect:
/// only reservations still <see cref="ReservationStatus.Reserved"/> change.
/// </remarks>
public sealed class StockReservation
{
    public StockReservation(Guid orderId, ProductVariantId variantId, int quantity, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        VariantId = variantId;
        Quantity = quantity;
        Status = ReservationStatus.Reserved;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The Orders module's order id; the Catalog knows it only as an id.</summary>
    public Guid OrderId { get; private set; }

    public ProductVariantId VariantId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>If still reserved after this, the units go back to stock (e.g. an order that was never saved).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }
}
