using Loja.Orders.Contracts;
using Loja.Payments.Domain;
using Loja.SharedKernel;

namespace Loja.Payments.Application.CreatePixCharge;

/// <summary>What the order page shows about the payment.</summary>
/// <param name="Status">"Pending", "Paid" or "Expired".</param>
public sealed record PaymentView(
    Guid PaymentId,
    Guid OrderId,
    string Gateway,
    string Status,
    decimal Amount,
    string Currency,
    string PixCopyPaste,
    string PixQrCodeImage,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Creates the Pix charge of an order, or returns the one still valid: refreshing the order page
/// must not create a new charge each time.
/// </summary>
public sealed class CreatePixChargeHandler(
    IOrdersApi orders,
    IPaymentRepository payments,
    IPaymentGatewayResolver gateways,
    TimeProvider clock)
{
    /// <summary>How long the shopper has to pay the Pix.</summary>
    public static readonly TimeSpan PixValidity = TimeSpan.FromMinutes(30);

    public static readonly Error GatewayUnavailable =
        Error.Failure("Payments.GatewayUnavailable", "Não foi possível gerar o Pix agora. Tente novamente em instantes.");

    public static readonly Error OrderCannotBePaid =
        Error.Conflict("Payments.OrderCannotBePaid", "Este pedido não aguarda pagamento.");

    public async Task<Result<PaymentView>> HandleAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(orderId, cancellationToken);

        // Another customer's order is "not found": it must not reveal that it exists.
        if (order is null || order.CustomerId != customerId)
        {
            return PaymentErrors.NotFound;
        }

        var now = clock.GetUtcNow();
        var latest = await payments.GetLatestForOrderAsync(orderId, cancellationToken);

        // A charge already paid, or still valid: show it again.
        if (latest is { Status: PaymentStatus.Paid } || latest is { Status: PaymentStatus.Pending } && latest.ExpiresAt > now)
        {
            return latest.ToView();
        }

        if (!order.CanBePaid)
        {
            return OrderCannotBePaid;
        }

        var gateway = gateways.Default;
        var amount = Money.Of(order.Total, order.Currency);
        PixCharge charge;
        try
        {
            charge = await gateway.CreatePixChargeAsync(
                new PixChargeRequest(orderId, amount, $"Pedido {order.Code} - Loja Demo", order.CustomerEmail, now + PixValidity),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The order exists anyway; the page offers "Gerar Pix novamente".
            return GatewayUnavailable;
        }

        var payment = Payment.Create(orderId, gateway.Name, amount, charge, now);
        payments.Add(payment);
        await payments.SaveChangesAsync(cancellationToken);

        return payment.ToView();
    }
}

public sealed class GetOrderPaymentHandler(IOrdersApi orders, IPaymentRepository payments)
{
    /// <returns>Null when the order has no payment yet (or is not the customer's).</returns>
    public async Task<PaymentView?> HandleAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(orderId, cancellationToken);
        if (order is null || order.CustomerId != customerId)
        {
            return null;
        }

        return (await payments.GetLatestForOrderAsync(orderId, cancellationToken))?.ToView();
    }
}

internal static class PaymentViewMapping
{
    public static PaymentView ToView(this Payment payment) =>
        new(payment.Id.Value, payment.OrderId, payment.Gateway, payment.Status.ToString(),
            payment.Amount.Amount, payment.Amount.Currency, payment.PixCopyPaste, payment.PixQrCodeImage, payment.ExpiresAt);
}
