using Loja.Customers.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Loja.Web.Account;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // POST with an antiforgery token: a GET link could be embedded in any other site
        // to sign people out without them asking.
        app.MapPost("/conta/sair", async Task<Results<RedirectHttpResult, BadRequest>> (
                HttpContext context,
                IAntiforgery antiforgery,
                SignInManager<Customer> signInManager) =>
            {
                // Checked explicitly: minimal APIs only reject an invalid token by themselves when the
                // endpoint binds form parameters, and this one has none.
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    return TypedResults.BadRequest();
                }

                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect("~/");
            })
            .DisableAntiforgery() // validated above; avoids validating twice
            .ExcludeFromDescription();

        return app;
    }
}

/// <summary>
/// Where to go after signing in. Only local paths are accepted: otherwise a link like
/// /conta/entrar?returnUrl=https://fake-store.example would send the customer to another site
/// after a legitimate login (an "open redirect").
/// </summary>
public static class SafeRedirect
{
    public static string LocalOrHome(string? url) =>
        url is ['/', ..] && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal)
            ? url
            : "/";
}
