using Loja.SharedKernel;

namespace Loja.Orders.Domain;

// Messages may reach the store or the admin panel, so they are in Brazilian Portuguese.
public static class OrderErrors
{
    public static readonly Error NoLines =
        Error.Validation("Orders.NoLines", "O pedido precisa de pelo menos um item.");

    public static readonly Error InvalidQuantity =
        Error.Validation("Orders.InvalidQuantity", "A quantidade de cada item deve ser maior que zero.");

    public static readonly Error MixedCurrencies =
        Error.Validation("Orders.MixedCurrencies", "Todos os itens do pedido devem estar na mesma moeda.");

    public static readonly Error AddressIncomplete =
        Error.Validation("Orders.AddressIncomplete", "Preencha todos os campos obrigatórios do endereço.");

    public static readonly Error InvalidState =
        Error.Validation("Orders.InvalidState", "UF inválida. Use a sigla do estado, por exemplo SP.");

    public static readonly Error NotFound =
        Error.NotFound("Orders.NotFound", "Pedido não encontrado.");

    public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
        Error.Conflict("Orders.InvalidTransition", $"Um pedido {Label(from)} não pode passar a {Label(to)}.");

    // Used only in messages; the store has its own labels for the screens.
    private static string Label(OrderStatus status) => status switch
    {
        OrderStatus.Created => "criado",
        OrderStatus.AwaitingPayment => "aguardando pagamento",
        OrderStatus.Paid => "pago",
        OrderStatus.Picking => "em separação",
        OrderStatus.Shipped => "enviado",
        OrderStatus.Delivered => "entregue",
        OrderStatus.Cancelled => "cancelado",
        OrderStatus.Refunded => "reembolsado",
        _ => status.ToString(),
    };
}
