using Loja.SharedKernel;

namespace Loja.Payments.Contracts;

// Integration events of the Payments module, delivered through its outbox (at least once).

/// <summary>A Pix charge exists for the order: the order now waits for the payment.</summary>
public sealed record PixChargeCreatedIntegrationEvent(Guid OrderId, Guid PaymentId, DateTimeOffset ExpiresAt) : IntegrationEvent;

/// <summary>The gateway confirmed the payment of the order.</summary>
public sealed record PaymentConfirmedIntegrationEvent(Guid OrderId, Guid PaymentId, decimal Amount, string Currency) : IntegrationEvent;

/// <summary>The Pix was not paid in time.</summary>
public sealed record PaymentExpiredIntegrationEvent(Guid OrderId, Guid PaymentId) : IntegrationEvent;
