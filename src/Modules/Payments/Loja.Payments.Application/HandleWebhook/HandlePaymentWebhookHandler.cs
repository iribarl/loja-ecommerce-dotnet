using Loja.Payments.Domain;

namespace Loja.Payments.Application.HandleWebhook;

public enum WebhookOutcome
{
    /// <summary>Handled (whatever the payment state turned out to be).</summary>
    Accepted,

    /// <summary>This delivery was already handled.</summary>
    Duplicate,

    /// <summary>No gateway with that name.</summary>
    UnknownGateway,

    /// <summary>Not authentic: bad or old signature.</summary>
    Rejected,
}

/// <summary>
/// Handles a payment provider's notification. The webhook is only a hint that something changed:
/// the real state is asked to the provider, so a forged or altered notification can never mark
/// an order as paid.
/// </summary>
public sealed class HandlePaymentWebhookHandler(
    IPaymentGatewayResolver gateways,
    IPaymentRepository payments,
    TimeProvider clock)
{
    public async Task<WebhookOutcome> HandleAsync(string gatewayName, WebhookRequest request, CancellationToken cancellationToken)
    {
        var gateway = gateways.Find(gatewayName);
        if (gateway is null)
        {
            return WebhookOutcome.UnknownGateway;
        }

        // 1. Authenticity: signature and timestamp.
        var notification = gateway.ReadWebhook(request);
        if (notification is null)
        {
            return WebhookOutcome.Rejected;
        }

        // 2. Idempotency (fast path; the unique key below covers simultaneous deliveries).
        if (await payments.WasWebhookProcessedAsync(gateway.Name, notification.EventId, cancellationToken))
        {
            return WebhookOutcome.Duplicate;
        }

        var now = clock.GetUtcNow();
        var payment = await payments.GetByExternalIdAsync(gateway.Name, notification.ExternalPaymentId, cancellationToken);

        if (payment is not null)
        {
            // 3. The truth comes from the provider, not from the notification. If this call fails,
            //    the exception makes the endpoint answer 500 and nothing is recorded, so the
            //    provider delivers the notification again later.
            var status = await gateway.GetPaymentAsync(notification.ExternalPaymentId, cancellationToken);

            if (status is { State: GatewayPaymentState.Approved, PaidAmount: { } paidAmount })
            {
                // A different amount or a late payment is not confirmed: it stays for a person to check.
                payment.ConfirmPaid(paidAmount, now);
            }
        }

        // 4. The record of this delivery commits together with the payment change and its outbox event.
        payments.RecordWebhook(gateway.Name, notification.EventId, now);
        return await payments.SaveWebhookAsync(cancellationToken) ? WebhookOutcome.Accepted : WebhookOutcome.Duplicate;
    }
}
