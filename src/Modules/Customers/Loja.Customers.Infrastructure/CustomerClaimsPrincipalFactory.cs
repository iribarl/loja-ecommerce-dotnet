using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Loja.Customers.Infrastructure;

/// <summary>
/// Adds the customer's first name to the sign-in cookie, so pages can greet them
/// ("Olá, Maria") without a database query on every request.
/// </summary>
internal sealed class CustomerClaimsPrincipalFactory(UserManager<Customer> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<Customer>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Customer user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FirstName));
        return identity;
    }
}
