using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Tests.Domain;

/// <summary>Builds valid orders and walks them to any status.</summary>
internal static class OrderBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public static OrderLine Line(decimal unitPrice = 49.90m, int quantity = 1) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "CAM-BAS-PRETO-M", "Camiseta Básica Algodão",
            new Dictionary<string, string> { ["Cor"] = "Preto", ["Tamanho"] = "M" }, Money.Of(unitPrice), quantity);

    public static ShippingAddress Address() =>
        ShippingAddress.Create("Maria da Silva", "01310-100", "Avenida Paulista", "1000", "Apto 12",
            "Bela Vista", "São Paulo", "SP").Value;

    public static ShippingOption Shipping(decimal price = 12.90m) =>
        new("São Paulo (capital e região)", Money.Of(price), 1, 2);

    public static Order Placed(params OrderLine[] lines) =>
        Order.Place(
            OrderId.New(),
            new CustomerSnapshot(Guid.NewGuid(), "Maria da Silva", "maria@exemplo.com"),
            lines.Length > 0 ? lines : [Line()],
            Address(),
            Shipping(),
            Now).Value;

    /// <summary>An order in <paramref name="status"/>, reached through valid transitions only.</summary>
    public static Order InStatus(OrderStatus status)
    {
        var order = Placed();
        Result Step(Result result) => result.IsSuccess ? result : throw new InvalidOperationException(result.Error.Message);

        switch (status)
        {
            case OrderStatus.Created:
                break;
            case OrderStatus.Cancelled:
                Step(order.Cancel("Desisti", Now));
                break;
            default:
                Step(order.AwaitPayment(Now));
                if (status == OrderStatus.AwaitingPayment) break;
                Step(order.MarkPaid(Now));
                if (status == OrderStatus.Paid) break;
                if (status == OrderStatus.Refunded) { Step(order.Refund(Now)); break; }
                Step(order.StartPicking(Now));
                if (status == OrderStatus.Picking) break;
                Step(order.Ship("BR123456789", Now));
                if (status == OrderStatus.Shipped) break;
                Step(order.Deliver(Now));
                break;
        }

        order.ClearDomainEvents();
        return order;
    }
}
