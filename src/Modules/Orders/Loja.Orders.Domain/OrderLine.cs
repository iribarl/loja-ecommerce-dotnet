using Loja.SharedKernel;

namespace Loja.Orders.Domain;

/// <summary>
/// One item as it was bought: name, attributes and unit price are copied from the catalog at the
/// moment of the purchase, so later catalog changes never alter a past order.
/// </summary>
public sealed record OrderLine(
    Guid VariantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    IReadOnlyDictionary<string, string> Attributes,
    Money UnitPrice,
    int Quantity)
{
    public Money LineTotal => UnitPrice * Quantity;
}

/// <summary>How the order is delivered and what it costs.</summary>
/// <param name="Zone">Shown to the customer, e.g. "Sudeste e Sul".</param>
public sealed record ShippingOption(string Zone, Money Price, int MinDays, int MaxDays)
{
    // For EF Core materialization: it cannot pass a nested complex type (Price) to the constructor,
    // so it creates the object empty and sets the properties afterwards.
    private ShippingOption()
        : this(string.Empty, Money.Zero(), 0, 0)
    {
    }
}

/// <summary>Who placed the order, as they were at that moment (for receipts and the admin panel).</summary>
public sealed record CustomerSnapshot(Guid CustomerId, string Name, string Email);

/// <summary>One step of the order's history, for the timeline and for auditing.</summary>
public sealed record OrderStatusChange(OrderStatus Status, DateTimeOffset ChangedAt, string? Note);
