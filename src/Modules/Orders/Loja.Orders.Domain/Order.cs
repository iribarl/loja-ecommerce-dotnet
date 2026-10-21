using Loja.SharedKernel;

namespace Loja.Orders.Domain;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.CreateVersion7());

    /// <summary>Short code for the customer and support, e.g. "3F9A1C2B".</summary>
    public string ShortCode => Value.ToString("N")[^8..].ToUpperInvariant();
}

/// <summary>
/// Aggregate root of the Orders module. The status changes only through its methods, and each
/// one checks that the transition is allowed (no shipping an unpaid order).
/// </summary>
public sealed class Order : Entity<OrderId>
{
    public const int MaxTrackingCodeLength = 100;

    public const int MaxCancellationReasonLength = 500;

    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _history = [];

    private Order(OrderId id, CustomerSnapshot customer, ShippingAddress address, ShippingOption shipping, DateTimeOffset now)
        : base(id)
    {
        Customer = customer;
        ShippingAddress = address;
        Shipping = shipping;
        PlacedAt = now;
        Status = OrderStatus.Created;
    }

    // For EF Core materialization.
    private Order(OrderId id)
        : base(id)
    {
        Customer = null!;
        ShippingAddress = null!;
        Shipping = null!;
    }

    public CustomerSnapshot Customer { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; }

    public ShippingOption Shipping { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public string? TrackingCode { get; private set; }

    public string? CancellationReason { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<OrderStatusChange> History => _history.AsReadOnly();

    public Money Subtotal => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.LineTotal);

    public Money Total => Subtotal + Shipping.Price;

    public string Currency => _lines.Count > 0 ? _lines[0].UnitPrice.Currency : Money.DefaultCurrency;

    /// <summary>The customer may cancel while nothing has been paid.</summary>
    public bool CanBeCancelledByCustomer => Status is OrderStatus.Created or OrderStatus.AwaitingPayment;

    /// <param name="id">Chosen by the caller: stock is reserved under this id before the order is saved.</param>
    public static Result<Order> Place(
        OrderId id,
        CustomerSnapshot customer,
        IReadOnlyCollection<OrderLine> lines,
        ShippingAddress address,
        ShippingOption shipping,
        DateTimeOffset now)
    {
        if (lines.Count == 0)
        {
            return OrderErrors.NoLines;
        }

        if (lines.Any(line => line.Quantity <= 0))
        {
            return OrderErrors.InvalidQuantity;
        }

        var currencies = lines.Select(line => line.UnitPrice.Currency).Append(shipping.Price.Currency).Distinct().Count();
        if (currencies > 1)
        {
            return OrderErrors.MixedCurrencies;
        }

        var order = new Order(id, customer, address, shipping, now);
        order._lines.AddRange(lines);
        order._history.Add(new OrderStatusChange(OrderStatus.Created, now, null));
        order.Raise(new OrderPlaced(id, customer.CustomerId, order.Total));
        return order;
    }

    /// <summary>The payment was requested (e.g. a Pix charge was created). Phase 4.</summary>
    public Result AwaitPayment(DateTimeOffset now) =>
        MoveTo(OrderStatus.AwaitingPayment, now, null, OrderStatus.Created);

    public Result MarkPaid(DateTimeOffset now)
    {
        var result = MoveTo(OrderStatus.Paid, now, null, OrderStatus.AwaitingPayment);
        if (result.IsSuccess)
        {
            Raise(new OrderPaid(Id));
        }

        return result;
    }

    public Result StartPicking(DateTimeOffset now) =>
        MoveTo(OrderStatus.Picking, now, null, OrderStatus.Paid);

    public Result Ship(string trackingCode, DateTimeOffset now)
    {
        trackingCode = trackingCode?.Trim() ?? string.Empty;
        if (trackingCode.Length is 0 or > MaxTrackingCodeLength)
        {
            return OrderErrors.TrackingCodeRequired;
        }

        var result = MoveTo(OrderStatus.Shipped, now, $"Rastreio: {trackingCode}", OrderStatus.Picking);
        if (result.IsSuccess)
        {
            TrackingCode = trackingCode;
            Raise(new OrderShipped(Id, trackingCode));
        }

        return result;
    }

    public Result Deliver(DateTimeOffset now)
    {
        var result = MoveTo(OrderStatus.Delivered, now, null, OrderStatus.Shipped);
        if (result.IsSuccess)
        {
            Raise(new OrderDelivered(Id));
        }

        return result;
    }

    /// <summary>Cancelled by the customer (or the store) before payment.</summary>
    public Result Cancel(string reason, DateTimeOffset now) => CancelCore(reason, expired: false, now);

    /// <summary>The payment was not confirmed in time.</summary>
    public Result Expire(DateTimeOffset now) =>
        CancelCore("Pagamento não confirmado no prazo.", expired: true, now);

    public Result Refund(DateTimeOffset now)
    {
        var result = MoveTo(OrderStatus.Refunded, now, null, OrderStatus.Paid);
        if (result.IsSuccess)
        {
            Raise(new OrderRefunded(Id));
        }

        return result;
    }

    private Result CancelCore(string reason, bool expired, DateTimeOffset now)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > MaxCancellationReasonLength)
        {
            return OrderErrors.CancellationReasonRequired;
        }

        var allowedFrom = expired
            ? new[] { OrderStatus.AwaitingPayment }
            : [OrderStatus.Created, OrderStatus.AwaitingPayment];

        var result = MoveTo(OrderStatus.Cancelled, now, reason, allowedFrom);
        if (result.IsSuccess)
        {
            CancellationReason = reason;
            Raise(new OrderCancelled(Id, reason, expired));
        }

        return result;
    }

    // The single place where Status changes: every transition is checked and recorded.
    private Result MoveTo(OrderStatus target, DateTimeOffset now, string? note, params OrderStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            return OrderErrors.InvalidTransition(Status, target);
        }

        Status = target;
        _history.Add(new OrderStatusChange(target, now, note));
        return Result.Success();
    }
}
