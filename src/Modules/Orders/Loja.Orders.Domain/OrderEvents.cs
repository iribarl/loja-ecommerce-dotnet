using Loja.SharedKernel;

namespace Loja.Orders.Domain;

public sealed record OrderPlaced(OrderId OrderId, Guid CustomerId, Money Total) : DomainEvent;

/// <param name="Expired">True when the payment was not confirmed in time (not a customer decision).</param>
public sealed record OrderCancelled(OrderId OrderId, string Reason, bool Expired) : DomainEvent;

public sealed record OrderPaid(OrderId OrderId) : DomainEvent;

public sealed record OrderShipped(OrderId OrderId, string TrackingCode) : DomainEvent;

public sealed record OrderDelivered(OrderId OrderId) : DomainEvent;

public sealed record OrderRefunded(OrderId OrderId) : DomainEvent;
