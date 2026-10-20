namespace Loja.SharedKernel;

/// <summary>
/// Brazilian postal code (Código de Endereçamento Postal): 8 digits, written 01310-100.
/// The first digit identifies the region of the country.
/// </summary>
public sealed record Cep
{
    public static readonly Error Invalid =
        Error.Validation("Cep.Invalid", "CEP inválido. Use 8 dígitos, por exemplo 01310-100.");

    private Cep(string digits) => Digits = digits;

    /// <summary>The 8 digits, without hyphen: "01310100".</summary>
    public string Digits { get; }

    public int FirstDigit => Digits[0] - '0';

    /// <summary>Accepts "01310-100", "01310100" and surrounding spaces.</summary>
    public static Result<Cep> Create(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 9 && text[5] == '-')
        {
            text = text.Remove(5, 1);
        }

        return text.Length == 8 && text.All(char.IsAsciiDigit) && text != "00000000"
            ? new Cep(text)
            : Invalid;
    }

    public override string ToString() => $"{Digits[..5]}-{Digits[5..]}";
}
