using System.Security.Claims;

namespace Loja.Web.Account;

public static class CurrentCustomer
{
    /// <summary>The signed-in customer's id, from the auth cookie; null for visitors.</summary>
    public static Guid? IdOf(HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
