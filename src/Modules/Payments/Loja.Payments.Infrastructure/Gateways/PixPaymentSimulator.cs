using Loja.Payments.Application.HandleWebhook;
using Loja.Payments.Domain;
using Loja.Payments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loja.Payments.Infrastructure.Gateways;

/// <summary>
/// Development tool: "the shopper paid in their bank app". Available only where the fake
/// gateway is (never in production).
/// </summary>
public interface IPixPaymentSimulator
{
    /// <returns>False when the payment does not exist or is not a fake one.</returns>
    Task<bool> SimulatePaymentAsync(Guid paymentId, CancellationToken cancellationToken);
}

/// <summary>
/// Marks the charge as paid in the fake "bank" and delivers the signed webhook the provider would
/// send, through the real webhook handler: signature, idempotency and the status query all run.
/// </summary>
internal sealed class PixPaymentSimulator(
    PaymentsDbContext db,
    FakePixGateway gateway,
    HandlePaymentWebhookHandler webhooks) : IPixPaymentSimulator
{
    public async Task<bool> SimulatePaymentAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var id = new PaymentId(paymentId);
        var externalId = await db.Payments
            .AsNoTracking()
            .Where(p => p.Id == id && p.Gateway == FakePixGateway.GatewayName)
            .Select(p => p.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (externalId is null || !gateway.MarkAsPaid(externalId))
        {
            return false;
        }

        var outcome = await webhooks.HandleAsync(FakePixGateway.GatewayName, gateway.BuildWebhook(externalId), cancellationToken);
        return outcome is WebhookOutcome.Accepted or WebhookOutcome.Duplicate;
    }
}
