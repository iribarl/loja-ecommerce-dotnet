namespace Loja.Customers.Contracts;

/// <summary>
/// What other modules may ask about customers. Only the data they need: never passwords or tokens.
/// </summary>
public interface ICustomerDirectory
{
    /// <summary>Null when the customer does not exist.</summary>
    Task<CustomerInfo?> GetAsync(Guid customerId, CancellationToken cancellationToken);
}

public sealed record CustomerInfo(Guid CustomerId, string FullName, string Email);
