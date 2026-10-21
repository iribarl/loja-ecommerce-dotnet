using Loja.Catalog.Application.Products.Images;
using Loja.Customers.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Loja.Web.Admin;

/// <summary>
/// Image upload and removal for the product page of the panel.
/// </summary>
/// <remarks>
/// Plain HTML forms posting here: Blazor's InputFile needs an interactive page, and these pages
/// are static server rendering. Minimal APIs validate the antiforgery token of form posts
/// (the form renders it with &lt;AntiforgeryToken /&gt;).
/// </remarks>
public static class AdminImageEndpoints
{
    // Slightly above the use case's 2 MB, so a too-large image gets its friendly message
    // instead of a bare 413 from the server.
    private const long RequestLimit = 3 * 1024 * 1024;

    public static IEndpointRouteBuilder MapAdminImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/produtos/{productId:guid}/imagens")
            .RequireAuthorization(policy => policy.RequireRole(CustomerRoles.Admin))
            .ExcludeFromDescription();

        group.MapPost("/", async (
                Guid productId,
                IFormFile? arquivo,
                [FromForm] string? variante,
                AddProductImageHandler handler,
                CancellationToken ct) =>
            {
                if (arquivo is null || arquivo.Length == 0)
                {
                    return BackTo(productId, "Catalog.UnsupportedImage");
                }

                await using var content = arquivo.OpenReadStream();
                var result = await handler.HandleAsync(productId, content, variante, ct);
                return BackTo(productId, result.IsSuccess ? null : result.Error.Code);
            })
            .WithMetadata(new RequestSizeLimitAttribute(RequestLimit))
            .WithFormOptions(multipartBodyLengthLimit: RequestLimit);

        group.MapPost("/remover", async (Guid productId, [FromForm] string url, RemoveProductImageHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(productId, url, ct);
            return BackTo(productId, result.IsSuccess ? null : result.Error.Code);
        });

        return app;
    }

    // Post-Redirect-Get back to the product page, with the outcome in the URL.
    private static RedirectHttpResult BackTo(Guid productId, string? errorCode) =>
        TypedResults.LocalRedirect(errorCode is null
            ? $"/admin/produtos/{productId}?imagem=ok#imagens"
            : $"/admin/produtos/{productId}?imagemErro={Uri.EscapeDataString(errorCode)}#imagens");
}
