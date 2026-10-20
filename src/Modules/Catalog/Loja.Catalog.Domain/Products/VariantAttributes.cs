using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Products;

/// <summary>
/// What makes a variant different from its siblings, e.g. {"Tamanho": "M", "Cor": "Azul"}.
/// Generic key/value pairs so the catalog works for any kind of store.
/// </summary>
public sealed class VariantAttributes : IEquatable<VariantAttributes>
{
    public static readonly VariantAttributes None = new(new SortedDictionary<string, string>(StringComparer.Ordinal));

    // Sorted so that equality and serialization do not depend on insertion order.
    private readonly SortedDictionary<string, string> _values;

    private VariantAttributes(SortedDictionary<string, string> values) => _values = values;

    public IReadOnlyDictionary<string, string> Values => _values;

    public static Result<VariantAttributes> Create(IEnumerable<KeyValuePair<string, string>> values)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return CatalogErrors.InvalidAttribute;
            }

            if (!sorted.TryAdd(key.Trim(), value.Trim()))
            {
                return CatalogErrors.InvalidAttribute;
            }
        }

        return new VariantAttributes(sorted);
    }

    public bool Equals(VariantAttributes? other) =>
        other is not null
        && _values.Count == other._values.Count
        && _values.All(pair => other._values.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as VariantAttributes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in _values)
        {
            hash.Add(key);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(", ", _values.Select(pair => $"{pair.Key}: {pair.Value}"));
}
