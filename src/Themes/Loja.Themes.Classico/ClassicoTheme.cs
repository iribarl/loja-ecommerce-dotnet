namespace Loja.Themes.Classico;

/// <summary>The original look of the store. It is the default theme and defines every part.</summary>
public static class ClassicoTheme
{
    public const string Name = "classico";

    public static readonly ThemeDefinition Definition = new(
        Name,
        "Clássico",
        typeof(StoreLayout),
        Stylesheets: [],
        Home: typeof(HomeContent),
        ProductCard: typeof(ProductCard),
        ProductView: typeof(ProductView));
}
