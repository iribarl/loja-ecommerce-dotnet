using Loja.Payments.Domain;
using Loja.SharedKernel;

namespace Loja.Payments.Application;

/// <summary>
/// A payment provider (fake for development, Mercado Pago, later others). Strategy pattern: the use
/// cases talk to this interface and never know which provider is behind it.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Short name used in URLs and stored with each payment, e.g. "mercadopago".</summary>
    string Name { get; }

    Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken);

    /// <summary>The real state of a charge, asked to the provider (webhooks are only a hint).</summary>
    Task<GatewayPaymentStatus> GetPaymentAsync(string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks that a webhook really comes from the provider (signature, timestamp) and extracts what
    /// it refers to. Null when the request is not authentic.
    /// </summary>
    WebhookNotification? ReadWebhook(WebhookRequest request);
}

/// <param name="Description">Shown in the shopper's bank app, e.g. "Pedido 3F9A1C2B - Loja Demo".</param>
public sealed record PixChargeRequest(Guid OrderId, Money Amount, string Description, string PayerEmail, DateTimeOffset ExpiresAt);

public enum GatewayPaymentState
{
    Pending,
    Approved,

    /// <summary>Cancelled, rejected or expired at the provider.</summary>
    NotPaid,
}

public sealed record GatewayPaymentStatus(string ExternalId, GatewayPaymentState State, Money? PaidAmount);

/// <summary>
/// The parts of an HTTP request a gateway needs to check a webhook, without ASP.NET types
/// (Application does not depend on the web framework).
/// </summary>
public sealed record WebhookRequest(
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    string Body);

/// <param name="EventId">Unique per notification: used to ignore repeated deliveries.</param>
/// <param name="ExternalPaymentId">The charge the notification is about.</param>
public sealed record WebhookNotification(string EventId, string ExternalPaymentId);

/// <summary>Picks the gateway by name (webhooks) or the one configured for new charges.</summary>
public interface IPaymentGatewayResolver
{
    IPaymentGateway Default { get; }

    IPaymentGateway? Find(string name);
}
