using Microsoft.AspNetCore.Components;

namespace Loja.Themes;

/// <summary>
/// A store theme: its layout, the parts of the store it replaces and its stylesheets.
/// Each theme project exposes one (e.g. ClassicoTheme.Definition) and the host registers it.
/// </summary>
/// <param name="Name">Identifier used in configuration and URLs, e.g. "classico".</param>
/// <param name="DisplayName">Name shown to people, e.g. "Clássico".</param>
/// <param name="Layout">Header, navigation and footer around every store page; inherits <see cref="StoreLayoutBase"/>.</param>
/// <param name="Stylesheets">Paths of the theme's CSS, e.g. "_content/Loja.Themes.Classico/theme.css".</param>
/// <param name="Home">Content of the home page (<see cref="HomeContentBase"/>); null keeps the default theme's.</param>
/// <param name="ProductCard">A product in listings (<see cref="ProductCardBase"/>); null keeps the default theme's.</param>
/// <param name="ProductView">The product page below the breadcrumb (<see cref="ProductViewBase"/>); null keeps the default theme's.</param>
public sealed record ThemeDefinition(
    string Name,
    string DisplayName,
    Type Layout,
    IReadOnlyList<string> Stylesheets,
    Type? Home = null,
    Type? ProductCard = null,
    Type? ProductView = null)
{
    /// <summary>Fails at start-up, not on the first visit, when a theme points at the wrong kind of component.</summary>
    public void Validate()
    {
        Check(Layout, typeof(StoreLayoutBase), nameof(Layout));
        Check(Home, typeof(HomeContentBase), nameof(Home));
        Check(ProductCard, typeof(ProductCardBase), nameof(ProductCard));
        Check(ProductView, typeof(ProductViewBase), nameof(ProductView));
    }

    private void Check(Type? component, Type requiredBase, string slot)
    {
        if (component is not null && !requiredBase.IsAssignableFrom(component))
        {
            throw new InvalidOperationException(
                $"Theme '{Name}': {slot} must be a component deriving from {requiredBase.Name}, but {component.Name} is not.");
        }
    }
}

/// <summary>The theme of the current request, with every part resolved (the theme's own or the default's).</summary>
public interface IThemeAccessor
{
    ActiveTheme Current { get; }

    /// <summary>Every registered theme, the default first.</summary>
    IReadOnlyList<ThemeDefinition> All { get; }
}

/// <summary>A theme with no gaps: parts it does not replace come from the default theme.</summary>
public sealed record ActiveTheme(
    string Name,
    string DisplayName,
    Type Layout,
    IReadOnlyList<string> Stylesheets,
    Type Home,
    Type ProductCard,
    Type ProductView)
{
    public static ActiveTheme From(ThemeDefinition theme, ThemeDefinition fallback) =>
        new(theme.Name,
            theme.DisplayName,
            theme.Layout,
            theme.Stylesheets,
            theme.Home ?? fallback.Home ?? throw Missing(fallback, nameof(theme.Home)),
            theme.ProductCard ?? fallback.ProductCard ?? throw Missing(fallback, nameof(theme.ProductCard)),
            theme.ProductView ?? fallback.ProductView ?? throw Missing(fallback, nameof(theme.ProductView)));

    private static InvalidOperationException Missing(ThemeDefinition fallback, string slot) =>
        new($"The default theme '{fallback.Name}' must define {slot}.");
}
