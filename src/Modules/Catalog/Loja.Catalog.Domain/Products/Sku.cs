using System.Text.RegularExpressions;
using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Products;

/// <summary>
/// Stock keeping unit: the code that identifies one sellable variant, e.g. "CAM-BAS-AZ-M".
/// </summary>
public sealed partial record Sku
{
    public const int MaxLength = 64;

    private Sku(string value) => Value = value;

    public string Value { get; }

    public static Result<Sku> Create(string value)
    {
        var sku = value?.Trim().ToUpperInvariant() ?? string.Empty;

        return sku.Length is > 0 and <= MaxLength && ValidFormat().IsMatch(sku)
            ? new Sku(sku)
            : CatalogErrors.InvalidSku;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex ValidFormat();
}
