using System.Globalization;

namespace Loja.Web;

/// <summary>
/// How the store shows values to Brazilian shoppers.
/// </summary>
public static class Formatting
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"R$ 49,90" for BRL; other currencies keep their ISO code.</summary>
    public static string Price(decimal amount, string currency) =>
        currency == "BRL"
            ? amount.ToString("C", PtBr)
            : $"{amount.ToString("N2", PtBr)} {currency}";
}
