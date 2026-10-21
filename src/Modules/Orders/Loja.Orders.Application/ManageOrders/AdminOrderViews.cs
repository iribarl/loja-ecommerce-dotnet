using Loja.Orders.Application.GetOrders;
using Loja.Orders.Domain;

namespace Loja.Orders.Application.ManageOrders;

/// <summary>An order in the admin list.</summary>
public sealed record AdminOrderSummaryView(
    Guid Id,
    string Code,
    DateTimeOffset PlacedAt,
    string Status,
    string CustomerName,
    string CustomerEmail,
    int Units,
    decimal Total,
    string Currency);

/// <summary>An order on its admin page: what the customer sees, who bought it, and what can be done now.</summary>
/// <param name="Actions">The <see cref="OrderAction"/>s its current status allows, in display order.</param>
public sealed record AdminOrderView(OrderView Order, string CustomerName, string CustomerEmail, IReadOnlyList<OrderAction> Actions);

/// <summary>What the store can do with an order. Each one is a method of the Order aggregate.</summary>
public enum OrderAction
{
    StartPicking,
    Ship,
    Deliver,
    Cancel,
}

internal static class AdminOrderMapping
{
    public static AdminOrderSummaryView ToAdminSummary(this Order order) =>
        new(order.Id.Value, order.Id.ShortCode, order.PlacedAt, order.Status.ToString(), order.Customer.Name,
            order.Customer.Email, order.Lines.Sum(l => l.Quantity), order.Total.Amount, order.Total.Currency);

    public static AdminOrderView ToAdminView(this Order order) =>
        new(order.ToView(), order.Customer.Name, order.Customer.Email, ActionsFor(order.Status));

    // Mirrors the transitions the aggregate allows, to show only buttons that can work.
    // The aggregate still checks each one: a stale page cannot force a wrong transition.
    private static OrderAction[] ActionsFor(OrderStatus status) => status switch
    {
        OrderStatus.Created or OrderStatus.AwaitingPayment => [OrderAction.Cancel],
        OrderStatus.Paid => [OrderAction.StartPicking],
        OrderStatus.Picking => [OrderAction.Ship],
        OrderStatus.Shipped => [OrderAction.Deliver],
        _ => [],
    };
}
