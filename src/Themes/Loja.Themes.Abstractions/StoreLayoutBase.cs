using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Loja.Themes;

/// <summary>What every theme layout shows about the visitor.</summary>
/// <param name="FirstName">"Maria" when signed in; null for visitors.</param>
/// <param name="LoginUrl">Sign-in page that comes back to the current page.</param>
public sealed record StoreSession(string? FirstName, bool IsAdmin, int CartUnits, string LoginUrl)
{
    public bool IsSignedIn => FirstName is not null;

    /// <summary>Signing out is a POST with an antiforgery token: a plain link would let other sites sign people out.</summary>
    public const string LogoutUrl = "/conta/sair";
}

/// <summary>Builds the <see cref="StoreSession"/> of a request. Implemented by the host, which knows cookies and Identity.</summary>
public interface IStoreSessionProvider
{
    Task<StoreSession> GetAsync(HttpContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Base of every theme layout: loads the visitor's session; the theme only writes the markup
/// (header, navigation, footer) around <see cref="LayoutComponentBase.Body"/>.
/// </summary>
/// <remarks>
/// The markup must keep the cart badge as an element with <c>data-cart-count</c>, so js/cart.js can
/// update it in place, and include &lt;StoreErrorUi /&gt;.
/// </remarks>
public abstract class StoreLayoutBase : LayoutComponentBase
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    [Inject]
    private IStoreSessionProvider SessionProvider { get; set; } = default!;

    protected StoreSession Session { get; private set; } = new(null, false, 0, "/conta/entrar");

    protected override async Task OnInitializedAsync()
    {
        if (HttpContext is not null)
        {
            Session = await SessionProvider.GetAsync(HttpContext, HttpContext.RequestAborted);
        }
    }
}
