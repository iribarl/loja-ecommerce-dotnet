using System.Security.Claims;
using Loja.Cart.Application.GetCartUnits;
using Loja.Customers.Contracts;
using Loja.Customers.Infrastructure;
using Loja.Themes;
using Loja.Web.Cart;

namespace Loja.Web.Theming;

/// <summary>
/// What the theme layouts show about the visitor: name and role from the sign-in cookie (no
/// database query), and the number of units in the cart.
/// </summary>
internal sealed class StoreSessionProvider(IServiceScopeFactory scopeFactory) : IStoreSessionProvider
{
    public async Task<StoreSession> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var user = context.User;
        var firstName = user.Identity?.IsAuthenticated == true ? user.FindFirst(ClaimTypes.GivenName)?.Value : null;

        // Blazor does not wait for the layout before initializing the page, so both run at the same
        // time. The page may use the cart database too (e.g. /carrinho), and a DbContext cannot run
        // two operations at once: the layout gets its own scope and DbContext.
        await using var scope = scopeFactory.CreateAsyncScope();
        var cartUnits = await scope.ServiceProvider.GetRequiredService<GetCartUnitsHandler>()
            .HandleAsync(CartCookie.OwnerOf(context), cancellationToken);

        // After signing in, come back to the page the visitor was on.
        var loginUrl = $"{CustomersModule.LoginPath}?{CustomersModule.ReturnUrlParameter}={Uri.EscapeDataString(context.Request.Path.Value ?? "/")}";

        return new StoreSession(firstName, user.IsInRole(CustomerRoles.Admin), cartUnits, loginUrl);
    }
}
