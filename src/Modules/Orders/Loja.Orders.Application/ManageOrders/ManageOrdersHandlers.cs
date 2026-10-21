using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Application.ManageOrders;

public sealed class ListAdminOrdersHandler(IOrderRepository orders)
{
    public const int PageSize = 20;

    /// <param name="status">A status name (see GetOrders.OrderStatusNames); unknown or empty means all.</param>
    public async Task<PagedResult<AdminOrderSummaryView>> HandleAsync(
        string? status, string? code, int page, CancellationToken cancellationToken)
    {
        OrderStatus? statusFilter = Enum.TryParse<OrderStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
        var search = new OrderSearch(statusFilter, string.IsNullOrWhiteSpace(code) ? null : code.Trim(), Math.Max(page, 1), PageSize);

        var result = await orders.SearchAsync(search, cancellationToken);
        return new PagedResult<AdminOrderSummaryView>(
            result.Items.Select(o => o.ToAdminSummary()).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>Paid orders nobody started picking yet: the panel's to-do.</summary>
    public Task<int> CountToPickAsync(CancellationToken cancellationToken) =>
        orders.CountAsync(OrderStatus.Paid, cancellationToken);
}

public sealed class GetAdminOrderHandler(IOrderRepository orders)
{
    public async Task<Result<AdminOrderView>> HandleAsync(Guid orderId, CancellationToken cancellationToken) =>
        await orders.GetByIdAsync(new OrderId(orderId), cancellationToken) is { } order
            ? order.ToAdminView()
            : OrderErrors.NotFound;
}

/// <summary>
/// The store moves an order forward (picking, shipped, delivered) or cancels an unpaid one.
/// Every change goes through the Order aggregate, which checks the transition.
/// </summary>
public sealed class ChangeOrderStatusHandler(IOrderRepository orders, TimeProvider clock)
{
    public const string StoreReasonPrefix = "Cancelado pela loja: ";

    /// <param name="text">The tracking code for <see cref="OrderAction.Ship"/>, the reason for <see cref="OrderAction.Cancel"/>.</param>
    public async Task<Result> HandleAsync(Guid orderId, OrderAction action, string? text, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(new OrderId(orderId), cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var now = clock.GetUtcNow();
        var result = action switch
        {
            OrderAction.StartPicking => order.StartPicking(now),
            OrderAction.Ship => order.Ship(text ?? string.Empty, now),
            OrderAction.Deliver => order.Deliver(now),
            OrderAction.Cancel => string.IsNullOrWhiteSpace(text)
                ? OrderErrors.CancellationReasonRequired
                : order.Cancel(StoreReasonPrefix + text.Trim(), now),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        if (result.IsFailure)
        {
            return result;
        }

        // The order may have changed after the page was shown (e.g. paid or cancelled meanwhile).
        return await orders.SaveIfUnchangedAsync(cancellationToken) ? Result.Success() : OrderErrors.ChangedMeanwhile;
    }
}
