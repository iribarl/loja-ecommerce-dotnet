using Loja.Orders.Domain;

namespace Loja.Orders.Application.GetOrders;

// Views are the Application's own types: they never expose domain types, so the domain can change
// without breaking the screens (and the web layer does not depend on the domain).

/// <summary>An order in the customer's list.</summary>
/// <param name="Status">Name of the status, e.g. "AwaitingPayment" (see <see cref="OrderStatusNames"/>).</param>
public sealed record OrderSummaryView(
    Guid Id,
    string Code,
    DateTimeOffset PlacedAt,
    string Status,
    int Units,
    decimal Total,
    string Currency);

/// <summary>An order on its own page.</summary>
public sealed record OrderView(
    Guid Id,
    string Code,
    DateTimeOffset PlacedAt,
    string Status,
    bool CanBeCancelled,
    IReadOnlyList<OrderLineView> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    string ShippingZone,
    int ShippingMinDays,
    int ShippingMaxDays,
    AddressView Address,
    string? TrackingCode,
    string? CancellationReason,
    IReadOnlyList<StatusChangeView> History);

public sealed record OrderLineView(
    string ProductName,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record AddressView(
    string Recipient,
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State);

public sealed record StatusChangeView(string Status, DateTimeOffset ChangedAt, string? Note);

/// <summary>The status names used in the views.</summary>
public static class OrderStatusNames
{
    public const string Created = nameof(OrderStatus.Created);
    public const string AwaitingPayment = nameof(OrderStatus.AwaitingPayment);
    public const string Paid = nameof(OrderStatus.Paid);
    public const string Picking = nameof(OrderStatus.Picking);
    public const string Shipped = nameof(OrderStatus.Shipped);
    public const string Delivered = nameof(OrderStatus.Delivered);
    public const string Cancelled = nameof(OrderStatus.Cancelled);
    public const string Refunded = nameof(OrderStatus.Refunded);
}

internal static class OrderViewMapping
{
    public static OrderSummaryView ToSummary(this Order order) =>
        new(order.Id.Value, order.Id.ShortCode, order.PlacedAt, order.Status.ToString(),
            order.Lines.Sum(l => l.Quantity), order.Total.Amount, order.Total.Currency);

    public static OrderView ToView(this Order order)
    {
        var address = order.ShippingAddress;

        return new OrderView(
            order.Id.Value,
            order.Id.ShortCode,
            order.PlacedAt,
            order.Status.ToString(),
            order.CanBeCancelledByCustomer,
            order.Lines.Select(l => new OrderLineView(
                l.ProductName, l.Sku, l.Attributes, l.UnitPrice.Amount, l.Quantity, l.LineTotal.Amount)).ToList(),
            order.Subtotal.Amount,
            order.Shipping.Price.Amount,
            order.Total.Amount,
            order.Total.Currency,
            order.Shipping.Zone,
            order.Shipping.MinDays,
            order.Shipping.MaxDays,
            new AddressView(address.Recipient, address.Cep.ToString(), address.Street, address.Number,
                address.Complement, address.District, address.City, address.State),
            order.TrackingCode,
            order.CancellationReason,
            order.History.Select(h => new StatusChangeView(h.Status.ToString(), h.ChangedAt, h.Note)).ToList());
    }
}
