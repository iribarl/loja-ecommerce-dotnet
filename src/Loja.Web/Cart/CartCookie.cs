using System.Security.Claims;
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
            Issue(context);
        }

        return next(context);
    }

    /// <summary>The anonymous cart id of this request, if the visitor has one.</summary>
    public static Guid? AnonymousCartIdOf(HttpContext context) =>
        context.Items[ItemKey] is Guid issued ? issued
        : TryRead(context.Request, out var existing) ? existing
        : null;

    /// <summary>
    /// Gives the visitor a new, empty anonymous cart. Used at sign-in: the old anonymous cart now
    /// belongs to the customer and must not be reachable from this browser after signing out.
    /// </summary>
    public static void Renew(HttpContext context) => Issue(context);

    private static void Issue(HttpContext context)
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

    /// <summary>
    /// Who is shopping in this request: the anonymous cart from the cookie and, when signed in,
    /// the customer (whose own cart is then the one used).
    /// </summary>
    public static CartOwner OwnerOf(HttpContext context)
    {
        var cartId = AnonymousCartIdOf(context) ?? Guid.Empty;

        var customerId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : (Guid?)null;

        return new CartOwner(cartId, customerId);
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
