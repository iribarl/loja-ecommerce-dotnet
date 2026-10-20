namespace Loja.Catalog.Application.Products.SearchProducts;

/// <summary>
/// Search filters as they come from the store page or the HTTP API. All optional.
/// </summary>
/// <param name="Attributes">Variant attributes to match, e.g. {"Tamanho": "M"}.</param>
public sealed record SearchProductsRequest(
    string? Text = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    IReadOnlyDictionary<string, string>? Attributes = null,
    bool InStockOnly = false,
    int Page = 1,
    int PageSize = SearchProductsHandler.DefaultPageSize);
