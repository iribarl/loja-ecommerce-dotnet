namespace Loja.Orders.Contracts;

/// <summary>
/// What other modules may ask the Orders module.
/// </summary>
public interface IOrdersApi
{
    /// <summary>What a payment needs to know about an order; null when it does not exist.</summary>
    Task<OrderForPayment?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <param name="Code">Short code shown to the customer, e.g. "3F9A1C2B".</param>
/// <param name="CanBePaid">True while the order waits for its payment (not paid, not cancelled).</param>
public sealed record OrderForPayment(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    string Code,
    decimal Total,
    string Currency,
    bool CanBePaid);
