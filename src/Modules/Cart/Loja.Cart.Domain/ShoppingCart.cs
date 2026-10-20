using Loja.SharedKernel;

namespace Loja.Cart.Domain;

public readonly record struct CartId(Guid Value)
{
    /// <summary>
    /// Random (v4) on purpose: the id of an anonymous cart travels in a cookie and must not be guessable.
    /// </summary>
    public static CartId New() => new(Guid.NewGuid());
}

/// <summary>
/// A shopping cart. It is anonymous (identified only by the id in the visitor's cookie)
/// until it is assigned to a customer when they sign in.
/// </summary>
/// <remarks>
/// The cart enforces its own rules (quantities, limits, merging). Whether a product exists or
/// has stock is asked to the catalog by the use cases, which know about other modules.
/// </remarks>
public sealed class ShoppingCart : Entity<CartId>
{
    public const int MaxUnitsPerItem = 10;

    private readonly List<CartItem> _items = [];

    private ShoppingCart(CartId id, Guid? customerId, DateTimeOffset now)
        : base(id)
    {
        CustomerId = customerId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // For EF Core materialization: it sets the other properties after construction.
    private ShoppingCart(CartId id)
        : base(id)
    {
    }

    /// <summary>Null while the cart is anonymous.</summary>
    public Guid? CustomerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last change; used later to find abandoned carts.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>In the order they were added.</summary>
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public int TotalUnits => _items.Sum(item => item.Quantity);

    /// <param name="id">The id already stored in the visitor's cookie.</param>
    public static ShoppingCart CreateAnonymous(CartId id, DateTimeOffset now) => new(id, null, now);

    public static ShoppingCart CreateForCustomer(Guid customerId, DateTimeOffset now) => new(CartId.New(), customerId, now);

    public int QuantityOf(Guid variantId) => _items.Find(item => item.VariantId == variantId)?.Quantity ?? 0;

    /// <summary>Adds units; if the variant is already in the cart, the quantities are summed.</summary>
    public Result AddItem(Guid variantId, int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            return CartErrors.InvalidQuantity;
        }

        var item = _items.Find(i => i.VariantId == variantId);
        var total = (item?.Quantity ?? 0) + quantity;

        if (total > MaxUnitsPerItem)
        {
            return CartErrors.TooManyUnits;
        }

        if (item is null)
        {
            _items.Add(new CartItem(variantId, quantity, now));
        }
        else
        {
            item.ChangeQuantity(total);
        }

        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>Sets the quantity of a line; zero removes it.</summary>
    public Result SetQuantity(Guid variantId, int quantity, DateTimeOffset now)
    {
        if (quantity < 0)
        {
            return CartErrors.InvalidQuantity;
        }

        if (quantity > MaxUnitsPerItem)
        {
            return CartErrors.TooManyUnits;
        }

        var item = _items.Find(i => i.VariantId == variantId);
        if (item is null)
        {
            return CartErrors.ItemNotFound;
        }

        if (quantity == 0)
        {
            _items.Remove(item);
        }
        else
        {
            item.ChangeQuantity(quantity);
        }

        UpdatedAt = now;
        return Result.Success();
    }

    public Result RemoveItem(Guid variantId, DateTimeOffset now) => SetQuantity(variantId, 0, now);

    /// <summary>
    /// Makes an anonymous cart the customer's cart (used when they sign in without a cart of their own).
    /// </summary>
    public Result AssignTo(Guid customerId, DateTimeOffset now)
    {
        if (CustomerId is not null)
        {
            return CartErrors.AlreadyOwned;
        }

        CustomerId = customerId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Brings the lines of another cart into this one (when a visitor with an anonymous cart signs in
    /// and already had a cart). Quantities are summed and capped at <see cref="MaxUnitsPerItem"/>,
    /// so nothing the shopper chose is lost silently, except units over the limit.
    /// </summary>
    public void Merge(ShoppingCart other, DateTimeOffset now)
    {
        foreach (var incoming in other._items.OrderBy(i => i.AddedAt))
        {
            var item = _items.Find(i => i.VariantId == incoming.VariantId);
            if (item is null)
            {
                _items.Add(new CartItem(incoming.VariantId, Math.Min(incoming.Quantity, MaxUnitsPerItem), incoming.AddedAt));
            }
            else
            {
                item.ChangeQuantity(Math.Min(item.Quantity + incoming.Quantity, MaxUnitsPerItem));
            }
        }

        UpdatedAt = now;
    }
}
