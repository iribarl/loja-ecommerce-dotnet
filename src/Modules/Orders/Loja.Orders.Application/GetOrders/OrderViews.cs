using Loja.Orders.Domain;

namespace Loja.Orders.Application.GetOrders;

/// <summary>An order in the customer's list.</summary>
public sealed record OrderSummaryView(
    Guid Id,
    string Code,
    DateTimeOffset PlacedAt,
    OrderStatus Status,
    int Units,
    decimal Total,
    string Currency);

/// <summary>An order on its own page.</summary>
public sealed record OrderView(
    Guid Id,
    string Code,
    DateTimeOffset PlacedAt,
    OrderStatus Status,
    bool CanBeCancelled,
    IReadOnlyList<OrderLineView> Lines,
    decimal Subtotal,
    decimal Shipping,
    decimal Total,
    string Currency,
    string ShippingZone,
    int ShippingMinDays,
    int ShippingMaxDays,
    ShippingAddress Address,
    string? TrackingCode,
    string? CancellationReason,
    IReadOnlyList<OrderStatusChange> History);

public sealed record OrderLineView(
    string ProductName,
    string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

internal static class OrderViewMapping
{
    public static OrderSummaryView ToSummary(this Order order) =>
        new(order.Id.Value, order.Id.ShortCode, order.PlacedAt, order.Status,
            order.Lines.Sum(l => l.Quantity), order.Total.Amount, order.Total.Currency);

    public static OrderView ToView(this Order order) =>
        new(order.Id.Value,
            order.Id.ShortCode,
            order.PlacedAt,
            order.Status,
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
            order.ShippingAddress,
            order.TrackingCode,
            order.CancellationReason,
            order.History);
}
