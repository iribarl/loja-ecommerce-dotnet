using System.Globalization;

namespace Loja.Web;

/// <summary>
/// How the store shows values to Brazilian shoppers.
/// </summary>
public static class Formatting
{
    /// <summary>Brazilian Portuguese: dates as 20/10/2026, money as R$ 49,90.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"R$ 49,90" for BRL; other currencies keep their ISO code.</summary>
    public static string Price(decimal amount, string currency) =>
        currency == "BRL"
            ? amount.ToString("C", Culture)
            : $"{amount.ToString("N2", Culture)} {currency}";
}
