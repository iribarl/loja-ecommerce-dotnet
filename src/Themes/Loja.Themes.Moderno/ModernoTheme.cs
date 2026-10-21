namespace Loja.Themes.Moderno;

/// <summary>
/// A second look with its own structure. Its stylesheets start with Clássico's (pages it does not
/// redesign, such as the cart or checkout, keep that look) and its own rules come after.
/// </summary>
public static class ModernoTheme
{
    public const string Name = "moderno";

    public static readonly ThemeDefinition Definition = new(
        Name,
        "Moderno",
        typeof(StoreLayout),
        Stylesheets: ["_content/Loja.Themes.Classico/theme.css", "_content/Loja.Themes.Moderno/theme.css"],
        Home: typeof(HomeContent),
        ProductCard: typeof(ProductCard),
        ProductView: typeof(ProductView));
}
