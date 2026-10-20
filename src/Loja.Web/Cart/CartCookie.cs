using Loja.Cart.Application;

namespace Loja.Web.Cart;

/// <summary>
/// Gives every visitor an anonymous cart id in a cookie on their first page request.
/// </summary>
/// <remarks>
/// Interactive components (SignalR) cannot write cookies, so the id must already exist when a page
/// with an "add to cart" button is rendered. The database row is created only when something is added.
/// The cookie is strictly necessary for the store to work, so under the LGPD it needs no consent.
/// </remarks>
public sealed class CartCookie(RequestDelegate next)
{
    public const string Name = "loja_cart";

    private static readonly object ItemKey = new();

    public Task InvokeAsync(HttpContext context)
    {
        if (IsPageOrApiRequest(context.Request) && !TryRead(context.Request, out _))
        {
            var cartId = Guid.NewGuid();
            context.Response.Cookies.Append(Name, cartId.ToString("N"), new CookieOptions
            {
                HttpOnly = true,                      // not readable from JavaScript
                Secure = context.Request.IsHttps,     // HTTPS in the browser; tests use plain HTTP
                SameSite = SameSiteMode.Lax,          // sent when arriving from another site's link
                MaxAge = TimeSpan.FromDays(30),
                IsEssential = true,
            });

            // The browser sends the cookie from the next request on; this request uses it right away.
            context.Items[ItemKey] = cartId;
        }

        return next(context);
    }

    /// <summary>
    /// Who is shopping in this request. Customers (signed in) are added in a later step.
    /// </summary>
    public static CartOwner OwnerOf(HttpContext context)
    {
        var cartId = context.Items[ItemKey] is Guid issued
            ? issued
            : TryRead(context.Request, out var existing) ? existing : Guid.Empty;

        return new CartOwner(cartId);
    }

    private static bool TryRead(HttpRequest request, out Guid cartId) =>
        Guid.TryParseExact(request.Cookies[Name], "N", out cartId) && cartId != Guid.Empty;

    // Static files and Blazor's own endpoints (/_framework, /_blazor) do not need a cart.
    private static bool IsPageOrApiRequest(HttpRequest request) =>
        !request.Path.StartsWithSegments("/_framework")
        && !request.Path.StartsWithSegments("/_blazor")
        && !request.Path.StartsWithSegments("/_content")
        && !Path.HasExtension(request.Path.Value);
}
