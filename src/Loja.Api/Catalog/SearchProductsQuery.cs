using System.ComponentModel.DataAnnotations;
using Loja.Catalog.Application.Products.SearchProducts;

namespace Loja.Api.Catalog;

/// <summary>
/// Query string of GET /api/catalog/products, e.g. ?text=camiseta&amp;attr=Tamanho:M&amp;attr=Cor:Azul
/// </summary>
public sealed record SearchProductsQuery
{
    [StringLength(SearchProductsHandler.MaxTextLength)]
    public string? Text { get; init; }

    /// <summary>Category slug, e.g. "camisetas".</summary>
    public string? Category { get; init; }

    [Range(0, 1_000_000)]
    public decimal? MinPrice { get; init; }

    [Range(0, 1_000_000)]
    public decimal? MaxPrice { get; init; }

    public bool? InStockOnly { get; init; }

    [Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Range(1, SearchProductsHandler.MaxPageSize)]
    public int? PageSize { get; init; }

    /// <summary>Variant attributes as "Name:Value", e.g. attr=Tamanho:M.</summary>
    public string[]? Attr { get; init; }

    public bool TryToRequest(out SearchProductsRequest request, out string? invalidAttribute)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        invalidAttribute = null;

        foreach (var pair in Attr ?? [])
        {
            var separator = pair.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator == pair.Length - 1)
            {
                invalidAttribute = pair;
                request = new SearchProductsRequest();
                return false;
            }

            attributes[pair[..separator]] = pair[(separator + 1)..];
        }

        request = new SearchProductsRequest(
            Text,
            Category,
            MinPrice,
            MaxPrice,
            attributes,
            InStockOnly ?? false,
            Page ?? 1,
            PageSize ?? SearchProductsHandler.DefaultPageSize);
        return true;
    }
}
