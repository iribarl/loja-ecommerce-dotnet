using Loja.Shipping.Contracts;

namespace Loja.IntegrationTests;

/// <summary>
/// Replaces ViaCEP in the test apps: tests never call the internet.
/// </summary>
internal sealed class FakeAddressLookup : IAddressLookup
{
    /// <summary>A CEP the fake answers as "service down".</summary>
    public const string UnavailableCep = "88888-888";

    private static readonly Dictionary<string, AddressInfo> Addresses = new()
    {
        ["30130000"] = new("30130-000", "Avenida Afonso Pena", "Centro", "Belo Horizonte", "MG"),
        ["01310100"] = new("01310-100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP"),
    };

    public Task<AddressLookupResult> LookupAsync(string cep, CancellationToken cancellationToken)
    {
        var digits = new string(cep.Where(char.IsAsciiDigit).ToArray());

        return Task.FromResult(digits switch
        {
            { Length: not 8 } => AddressLookupResult.InvalidCep,
            "88888888" => AddressLookupResult.Unavailable,
            _ when Addresses.TryGetValue(digits, out var address) => AddressLookupResult.Found(address),
            _ => AddressLookupResult.NotFound,
        });
    }
}
