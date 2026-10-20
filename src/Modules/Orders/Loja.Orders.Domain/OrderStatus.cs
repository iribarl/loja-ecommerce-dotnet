namespace Loja.Orders.Domain;

/// <summary>
/// Life cycle of an order:
/// <code>
/// Created → AwaitingPayment → Paid → Picking → Shipped → Delivered
///    ↓            ↓ (expires)   ↓
/// Cancelled    Cancelled      Refunded
/// </code>
/// Only the methods of <see cref="Order"/> change it.
/// </summary>
public enum OrderStatus
{
    /// <summary>Criado: stock reserved, payment not requested yet.</summary>
    Created,

    /// <summary>Aguardando pagamento: the Pix charge exists and has not been paid.</summary>
    AwaitingPayment,

    /// <summary>Pago.</summary>
    Paid,

    /// <summary>Em separação: being picked and packed.</summary>
    Picking,

    /// <summary>Enviado.</summary>
    Shipped,

    /// <summary>Entregue.</summary>
    Delivered,

    /// <summary>Cancelado: by the customer, or because the payment expired.</summary>
    Cancelled,

    /// <summary>Reembolsado.</summary>
    Refunded,
}
