using Loja.Orders.Application.GetOrders;

namespace Loja.Web;

/// <summary>
/// How each order status is shown to the customer. The code uses English names;
/// the store speaks Brazilian Portuguese.
/// </summary>
public static class OrderStatusLabels
{
    public static string Label(string status) => status switch
    {
        OrderStatusNames.Created => "Pedido recebido",
        OrderStatusNames.AwaitingPayment => "Aguardando pagamento",
        OrderStatusNames.Paid => "Pago",
        OrderStatusNames.Picking => "Em separação",
        OrderStatusNames.Shipped => "Enviado",
        OrderStatusNames.Delivered => "Entregue",
        OrderStatusNames.Cancelled => "Cancelado",
        OrderStatusNames.Refunded => "Reembolsado",
        _ => status,
    };

    /// <summary>CSS class for the status badge: in progress, done or stopped.</summary>
    public static string CssClass(string status) => status switch
    {
        OrderStatusNames.Paid or OrderStatusNames.Picking or OrderStatusNames.Shipped or OrderStatusNames.Delivered => "status-ok",
        OrderStatusNames.Cancelled or OrderStatusNames.Refunded => "status-stopped",
        _ => "status-pending",
    };
}
