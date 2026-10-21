using Loja.Payments.Domain;

namespace Loja.Payments.Application.ExpirePixCharges;

/// <summary>
/// Expires the Pix charges nobody paid in time; their orders are then cancelled and the stock
/// released (through the outbox).
/// </summary>
/// <remarks>
/// Before expiring, the provider is asked: a customer may have paid at the last second with the
/// webhook still on its way. Cancelling a paid order would be the worst possible outcome.
/// </remarks>
public sealed class ExpirePixChargesHandler(
    IPaymentRepository payments,
    IPaymentGatewayResolver gateways,
    TimeProvider clock)
{
    private const int BatchSize = 50;

    /// <returns>How many payments were expired.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = 0;

        foreach (var payment in await payments.ListExpiredPendingAsync(now, BatchSize, cancellationToken))
        {
            var gateway = gateways.Find(payment.Gateway);
            var status = gateway is null ? null : await gateway.GetPaymentAsync(payment.ExternalId, cancellationToken);

            if (status is { State: GatewayPaymentState.Approved, PaidAmount: { } paidAmount })
            {
                // Paid in time; the webhook simply has not arrived yet.
                payment.ConfirmPaid(paidAmount, now);
            }
            else if (payment.Expire(now).IsSuccess)
            {
                expired++;
            }

            // If the webhook changed this payment meanwhile, skip it: the next run sees the new state.
            await payments.SaveIfUnchangedAsync(cancellationToken);
        }

        return expired;
    }
}
