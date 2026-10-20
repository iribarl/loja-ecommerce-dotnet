using Loja.SharedKernel;

namespace Loja.Orders.Contracts;

// Integration events of the Orders module, delivered through its outbox (at least once).
// Their shape is a promise to the consumers: add fields, never remove or rename them.

public sealed record OrderPlacedIntegrationEvent(Guid OrderId, Guid CustomerId, decimal Total, string Currency) : IntegrationEvent;

/// <param name="Expired">True when the payment was not confirmed in time.</param>
public sealed record OrderCancelledIntegrationEvent(Guid OrderId, string Reason, bool Expired) : IntegrationEvent;

public sealed record OrderPaidIntegrationEvent(Guid OrderId) : IntegrationEvent;

public sealed record OrderShippedIntegrationEvent(Guid OrderId, string TrackingCode) : IntegrationEvent;
