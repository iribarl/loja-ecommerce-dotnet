namespace Loja.Shipping.Contracts;

/// <summary>
/// Finds the street, district, city and state of a CEP, so the shopper does not type them.
/// </summary>
/// <remarks>
/// Backed by an external service: it may be slow or down. Callers must always let the shopper
/// type the address by hand when the lookup does not succeed.
/// </remarks>
public interface IAddressLookup
{
    Task<AddressLookupResult> LookupAsync(string cep, CancellationToken cancellationToken);
}

/// <param name="Street">May be empty: small towns have a single CEP for every street.</param>
public sealed record AddressInfo(string Cep, string Street, string District, string City, string State);

public enum AddressLookupStatus
{
    Found,

    /// <summary>A well-formed CEP that does not exist.</summary>
    NotFound,

    /// <summary>Not 8 digits; nothing was looked up.</summary>
    InvalidCep,

    /// <summary>The service did not answer in time or failed.</summary>
    Unavailable,
}

public sealed record AddressLookupResult(AddressLookupStatus Status, AddressInfo? Address = null)
{
    public static AddressLookupResult Found(AddressInfo address) => new(AddressLookupStatus.Found, address);

    public static readonly AddressLookupResult NotFound = new(AddressLookupStatus.NotFound);

    public static readonly AddressLookupResult InvalidCep = new(AddressLookupStatus.InvalidCep);

    public static readonly AddressLookupResult Unavailable = new(AddressLookupStatus.Unavailable);
}
