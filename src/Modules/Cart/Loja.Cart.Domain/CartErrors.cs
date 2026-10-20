using Loja.SharedKernel;

namespace Loja.Cart.Domain;

// Messages reach the store, so they are in Brazilian Portuguese.
public static class CartErrors
{
    public static readonly Error InvalidQuantity =
        Error.Validation("Cart.InvalidQuantity", "A quantidade deve ser maior que zero.");

    public static readonly Error TooManyUnits =
        Error.Validation("Cart.TooManyUnits", $"Você pode comprar no máximo {ShoppingCart.MaxUnitsPerItem} unidades de cada item.");

    public static readonly Error ItemNotFound =
        Error.NotFound("Cart.ItemNotFound", "Este item não está no carrinho.");

    public static readonly Error ProductUnavailable =
        Error.Validation("Cart.ProductUnavailable", "Este produto não está disponível.");

    public static readonly Error NotEnoughStock =
        Error.Validation("Cart.NotEnoughStock", "Não há estoque suficiente para esta quantidade.");

    public static readonly Error ChangedElsewhere =
        Error.Conflict("Cart.ChangedElsewhere", "Seu carrinho foi alterado em outra janela. Tente novamente.");

    public static readonly Error AlreadyOwned =
        Error.Conflict("Cart.AlreadyOwned", "Este carrinho já pertence a um cliente.");
}
