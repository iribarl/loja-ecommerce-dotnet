using Loja.Cart.Domain;
using Loja.Catalog.Contracts;
using Loja.SharedKernel;

namespace Loja.Cart.Application.GetCart;

public sealed class GetCartHandler(ICartRepository carts, ICatalogApi catalog)
{
    public async Task<CartView> HandleAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await owner.FindCartAsync(carts, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return CartView.Empty;
        }

        // One call for all the lines, with current prices and stock.
        var variants = (await catalog.GetVariantsAsync(cart.Items.Select(i => i.VariantId).ToList(), cancellationToken))
            .ToDictionary(v => v.VariantId);

        var lines = cart.Items
            .OrderBy(item => item.AddedAt)
            .Select(item => ToLine(item, variants.GetValueOrDefault(item.VariantId)))
            .ToList();

        // Money refuses to add different currencies, which would be a catalog bug.
        var subtotal = lines
            .Where(line => line.Issue is null)
            .Aggregate(Money.Zero(), (sum, line) => sum + Money.Of(line.LineTotal, line.Currency));

        return new CartView(
            lines,
            subtotal.Amount,
            subtotal.Currency,
            cart.TotalUnits,
            CanCheckout: lines.TrueForAll(line => line.Issue is null));
    }

    private static CartLineView ToLine(CartItem item, VariantSnapshot? variant)
    {
        if (variant is null)
        {
            // The variant no longer exists in the catalog: keep the line so the shopper can remove it.
            return new CartLineView(
                item.VariantId, "Produto indisponível", string.Empty, string.Empty,
                new Dictionary<string, string>(), null, 0m, item.Quantity, 0m, Money.DefaultCurrency,
                MaxQuantity: 0, CartLineIssue.Unavailable);
        }

        var issue = !variant.CanBePurchased ? CartLineIssue.Unavailable
            : item.Quantity > variant.AvailableStock ? CartLineIssue.NotEnoughStock
            : (CartLineIssue?)null;

        return new CartLineView(
            variant.VariantId,
            variant.ProductName,
            variant.ProductSlug,
            variant.Sku,
            variant.Attributes,
            variant.ImageUrl,
            variant.Price,
            item.Quantity,
            Money.Of(variant.Price, variant.Currency).Multiply(item.Quantity).Amount,
            variant.Currency,
            MaxQuantity: Math.Min(ShoppingCart.MaxUnitsPerItem, variant.AvailableStock),
            issue);
    }
}
