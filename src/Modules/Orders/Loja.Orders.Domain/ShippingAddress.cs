using Loja.SharedKernel;

namespace Loja.Orders.Domain;

/// <summary>
/// Where the order goes, as typed at checkout. A value object: an order keeps the address it was
/// placed with, even if the customer later changes theirs.
/// </summary>
public sealed record ShippingAddress
{
    // The 26 states and the Distrito Federal.
    private static readonly HashSet<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    private ShippingAddress(
        string recipient, Cep cep, string street, string number, string? complement,
        string district, string city, string state)
    {
        Recipient = recipient;
        Cep = cep;
        Street = street;
        Number = number;
        Complement = complement;
        District = district;
        City = city;
        State = state;
    }

    public string Recipient { get; }

    public Cep Cep { get; }

    /// <summary>Logradouro.</summary>
    public string Street { get; }

    /// <summary>Número (text: "S/N" and "120-A" exist).</summary>
    public string Number { get; }

    public string? Complement { get; }

    /// <summary>Bairro.</summary>
    public string District { get; }

    public string City { get; }

    /// <summary>UF, e.g. "SP".</summary>
    public string State { get; }

    public static Result<ShippingAddress> Create(
        string recipient, string cep, string street, string number, string? complement,
        string district, string city, string state)
    {
        if (Cep.Create(cep) is not { IsSuccess: true } validCep)
        {
            return Cep.Invalid;
        }

        string[] required = [recipient, street, number, district, city];
        if (required.Any(string.IsNullOrWhiteSpace))
        {
            return OrderErrors.AddressIncomplete;
        }

        var uf = state?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!States.Contains(uf))
        {
            return OrderErrors.InvalidState;
        }

        return new ShippingAddress(
            recipient.Trim(), validCep.Value, street.Trim(), number.Trim(),
            string.IsNullOrWhiteSpace(complement) ? null : complement.Trim(),
            district.Trim(), city.Trim(), uf);
    }
}
