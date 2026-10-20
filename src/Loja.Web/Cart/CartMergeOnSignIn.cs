using System.Security.Claims;
using Loja.Cart.Application.MergeCarts;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Loja.Web.Cart;

/// <summary>
/// Joins the visitor's anonymous cart with their customer cart when they sign in (login or sign-up).
/// </summary>
/// <remarks>
/// Wired here, in the host, through Identity's cookie events: the Customers module does not know
/// the Cart module and vice versa.
/// </remarks>
public static class CartMergeOnSignIn
{
    public static async Task OnSignedInAsync(CookieSignedInContext context)
    {
        var anonymousCartId = CartCookie.AnonymousCartIdOf(context.HttpContext);
        var customerId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (anonymousCartId is not { } cartId || !Guid.TryParse(customerId, out var customer))
        {
            return;
        }

        // Own scope: the sign-in page is using its own DbContext at this moment.
        await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();
        var merge = scope.ServiceProvider.GetRequiredService<MergeCartsHandler>();
        await merge.HandleAsync(cartId, customer, context.HttpContext.RequestAborted);

        CartCookie.Renew(context.HttpContext);
    }
}
