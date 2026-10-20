using Loja.Cart.Domain;

namespace Loja.Cart.Application;

/// <summary>
/// Cart rules the user interface needs to know (e.g. to fill a quantity selector),
/// so it does not depend on the domain.
/// </summary>
public static class CartRules
{
    public const int MaxUnitsPerItem = ShoppingCart.MaxUnitsPerItem;
}
