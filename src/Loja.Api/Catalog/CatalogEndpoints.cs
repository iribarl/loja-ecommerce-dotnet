using Loja.Catalog.Application.Categories;
using Loja.Catalog.Application.Categories.ListCategories;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetFilterOptions;
using Loja.Catalog.Application.Products.GetProductBySlug;
using Loja.Catalog.Application.Products.SearchProducts;
using Loja.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Loja.Api.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/catalog").WithTags("Catalog");

        group.MapGet("/products", SearchProducts)
            .WithName("SearchProducts")
            .WithSummary("Search published products with filters and paging.");

        group.MapGet("/products/{slug}", GetProduct)
            .WithName("GetProduct")
            .WithSummary("Get a published product by its slug, with variants and selector options.");

        group.MapGet("/categories", ListCategories)
            .WithName("ListCategories")
            .WithSummary("List all categories ordered by name.");

        group.MapGet("/filters", GetFilterOptions)
            .WithName("GetFilterOptions")
            .WithSummary("Categories and attribute values available to filter the search.");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<ProductSummary>>, ValidationProblem, ProblemHttpResult>> SearchProducts(
        [AsParameters] SearchProductsQuery query,
        SearchProductsHandler handler,
        CancellationToken cancellationToken)
    {
        if (!query.TryToRequest(out var request, out var invalidAttribute))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["attr"] = [$"'{invalidAttribute}' must have the form Name:Value."],
            });
        }

        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<Results<Ok<ProductPageResponse>, ProblemHttpResult>> GetProduct(
        string slug,
        GetProductBySlugHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(slug, cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<Ok<IReadOnlyList<CategorySummary>>> ListCategories(
        ListCategoriesHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Ok<FilterOptionsResponse>> GetFilterOptions(
        string? category,
        GetFilterOptionsHandler handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(category, cancellationToken));
}
