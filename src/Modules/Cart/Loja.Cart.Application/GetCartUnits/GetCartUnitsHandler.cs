namespace Loja.Cart.Application.GetCartUnits;

/// <summary>
/// Number of units in the cart, for the header badge. Does not ask the catalog: it must be cheap,
/// because it runs on every page.
/// </summary>
public sealed class GetCartUnitsHandler(ICartRepository carts)
{
    public async Task<int> HandleAsync(CartOwner owner, CancellationToken cancellationToken) =>
        (await owner.FindCartAsync(carts, cancellationToken))?.TotalUnits ?? 0;
}
