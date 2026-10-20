using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Loja.SharedKernel;

namespace Loja.Catalog.Domain;

/// <summary>
/// URL-friendly identifier, e.g. "Camiseta Básica" → "camiseta-basica".
/// </summary>
public sealed partial record Slug
{
    private Slug(string value) => Value = value;

    public string Value { get; }

    public static Result<Slug> From(string text)
    {
        var slug = Normalize(text ?? string.Empty);

        return slug.Length == 0
            ? CatalogErrors.InvalidSlug
            : new Slug(slug);
    }

    public override string ToString() => Value;

    private static string Normalize(string text)
    {
        // Split accented letters into letter + accent mark (á → a + ´) and drop the marks.
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var withoutAccents = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutAccents.Append(c);
            }
        }

#pragma warning disable CA1308 // Slugs are lowercase by convention.
        var lower = withoutAccents.ToString().ToLowerInvariant();
#pragma warning restore CA1308

        return NonAlphanumeric().Replace(lower, "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
