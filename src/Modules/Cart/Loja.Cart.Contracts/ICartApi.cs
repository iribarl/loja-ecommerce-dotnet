namespace Loja.Cart.Contracts;

/// <summary>
/// What other modules may ask the Cart: the checkout reads the customer's cart and empties it
/// once the order is placed.
/// </summary>
public interface ICartApi
{
    /// <summary>The lines of the customer's cart; empty when they have none.</summary>
    Task<IReadOnlyList<CartLine>> GetCustomerCartAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Empties the customer's cart. Does nothing when there is no cart.</summary>
    Task ClearCustomerCartAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed record CartLine(Guid VariantId, int Quantity);
