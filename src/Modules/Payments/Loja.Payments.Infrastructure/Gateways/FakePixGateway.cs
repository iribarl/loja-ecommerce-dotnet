using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Loja.Payments.Application;
using Loja.Payments.Domain;
using Loja.SharedKernel;

namespace Loja.Payments.Infrastructure.Gateways;

/// <summary>
/// A pretend Pix provider for development and tests: it creates charges, can be told that one was
/// paid, and signs its webhooks exactly like Mercado Pago. Never registered in production.
/// </summary>
/// <remarks>
/// Its "bank" lives in memory (singleton): charges are forgotten when the app restarts, which is
/// fine for a simulation.
/// </remarks>
internal sealed class FakePixGateway(string webhookSecret, TimeProvider clock) : IPaymentGateway
{
    public const string GatewayName = "fake";

    private readonly ConcurrentDictionary<string, FakeCharge> _charges = new();

    public string Name => GatewayName;

    public Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken)
    {
        var externalId = $"fake_{Guid.NewGuid():N}";
        _charges[externalId] = new FakeCharge(request.Amount, Paid: false);

        var code = PixCode.Build(
            pixKey: "pagamentos@loja-demo.example",
            amount: request.Amount.Amount,
            merchantName: "LOJA DEMO",
            merchantCity: "SAO PAULO",
            txId: externalId[^25..]);

        return Task.FromResult(new PixCharge(externalId, code, PixCode.PlaceholderQrDataUri(code), request.ExpiresAt));
    }

    public Task<GatewayPaymentStatus> GetPaymentAsync(string externalId, CancellationToken cancellationToken)
    {
        var status = _charges.TryGetValue(externalId, out var charge)
            ? new GatewayPaymentStatus(externalId, charge.Paid ? GatewayPaymentState.Approved : GatewayPaymentState.Pending,
                charge.Paid ? charge.Amount : null)
            : new GatewayPaymentStatus(externalId, GatewayPaymentState.NotPaid, null);

        return Task.FromResult(status);
    }

    public WebhookNotification? ReadWebhook(WebhookRequest request)
    {
        // Mercado Pago sends the payment id in the query (data.id) and a request id header.
        if (!request.Query.TryGetValue("data.id", out var dataId) ||
            !request.Headers.TryGetValue("x-request-id", out var requestId))
        {
            return null;
        }

        request.Headers.TryGetValue("x-signature", out var signature);
        return WebhookSignature.IsValid(webhookSecret, signature, dataId, requestId, clock.GetUtcNow())
            ? new WebhookNotification(requestId, dataId)
            : null;
    }

    /// <summary>The shopper "paid" in their bank app (development page). False for unknown charges.</summary>
    public bool MarkAsPaid(string externalId) =>
        _charges.TryGetValue(externalId, out var charge) && _charges.TryUpdate(externalId, charge with { Paid = true }, charge);

    /// <summary>The webhook the provider would send after a payment, signed with the shared secret.</summary>
    public WebhookRequest BuildWebhook(string externalId)
    {
        var requestId = Guid.NewGuid().ToString();
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds();

        return new WebhookRequest(
            new Dictionary<string, string>
            {
                ["x-request-id"] = requestId,
                ["x-signature"] = WebhookSignature.Sign(webhookSecret, externalId, requestId, timestamp),
            },
            new Dictionary<string, string> { ["data.id"] = externalId, ["type"] = "payment" },
            JsonSerializer.Serialize(new { type = "payment", action = "payment.updated", data = new { id = externalId } }));
    }

    private sealed record FakeCharge(Money Amount, bool Paid);
}

/// <summary>Picks the gateway configured for new charges, or the one a webhook names.</summary>
internal sealed class PaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways, string defaultGateway) : IPaymentGatewayResolver
{
    private readonly Dictionary<string, IPaymentGateway> _byName =
        gateways.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

    public IPaymentGateway Default => Find(defaultGateway)
        ?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
            $"Payment gateway '{defaultGateway}' is not registered."));

    public IPaymentGateway? Find(string name) => _byName.GetValueOrDefault(name);
}
