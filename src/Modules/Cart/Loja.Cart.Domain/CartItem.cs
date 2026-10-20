namespace Loja.Cart.Domain;

/// <summary>
/// One line of the cart: which variant and how many units. Prices are not stored here:
/// they are read from the catalog when the cart is shown, and frozen when the order is created.
/// </summary>
/// <remarks>Part of the <see cref="ShoppingCart"/> aggregate; changed only through it.</remarks>
public sealed class CartItem
{
    internal CartItem(Guid variantId, int quantity, DateTimeOffset addedAt)
    {
        VariantId = variantId;
        Quantity = quantity;
        AddedAt = addedAt;
    }

    /// <summary>Id of the catalog variant. The cart knows it only as an id.</summary>
    public Guid VariantId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Keeps the lines in the order they were added.</summary>
    public DateTimeOffset AddedAt { get; private set; }

    internal void ChangeQuantity(int quantity) => Quantity = quantity;
}
