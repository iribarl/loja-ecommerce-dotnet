namespace Loja.Web.Theming;

/// <summary>
/// Lets a visitor try another theme: "?tema=moderno" on any page switches to it and a cookie keeps
/// the choice; "?tema=padrao" goes back to the store's theme.
/// </summary>
/// <remarks>
/// A middleware rather than the page: with Blazor's static rendering the response may already be
/// streaming when a page runs, and cookies can no longer be written then. The cookie only holds a
/// display preference and is set by the visitor's own click, so under the LGPD it needs no banner.
/// </remarks>
public sealed class ThemeSelection(RequestDelegate next, ThemeCatalog catalog, IConfiguration configuration)
{
    public const string QueryName = "tema";
    public const string CookieName = "loja_tema";
    public const string StoreTheme = "padrao";

    private static readonly object ItemKey = new();

    private readonly bool _enabled = configuration.GetValue(Themes.PreviewConfigurationKey, defaultValue: true);

    public Task InvokeAsync(HttpContext context)
    {
        if (!_enabled)
        {
            return next(context);
        }

        var requested = context.Request.Query[QueryName].FirstOrDefault();
        if (requested is not null)
        {
            if (catalog.Find(requested) is { } theme)
            {
                context.Response.Cookies.Append(CookieName, theme.Name, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps, // HTTPS in the browser; tests use plain HTTP
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(30),
                    IsEssential = true,
                });
                context.Items[ItemKey] = theme.Name;
            }
            else if (requested == StoreTheme)
            {
                context.Response.Cookies.Delete(CookieName);
                context.Items[ItemKey] = StoreTheme;
            }
        }
        else if (context.Request.Cookies[CookieName] is { } remembered)
        {
            context.Items[ItemKey] = remembered;
        }

        return next(context);
    }

    /// <summary>The theme the visitor chose for this request; null to use the store's theme.</summary>
    public static string? ChosenBy(HttpContext? context) =>
        context?.Items[ItemKey] is string name && name != StoreTheme ? name : null;
}
