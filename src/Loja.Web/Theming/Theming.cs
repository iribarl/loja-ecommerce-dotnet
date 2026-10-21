using Loja.Themes;

namespace Loja.Web.Theming;

/// <summary>
/// The themes the store can use. The default theme defines every part; the others replace what
/// they want and fall back to the default for the rest.
/// </summary>
public sealed class ThemeCatalog
{
    private readonly Dictionary<string, ActiveTheme> _resolved;

    public ThemeCatalog(ThemeDefinition defaultTheme, IEnumerable<ThemeDefinition> others)
    {
        All = [defaultTheme, .. others];
        foreach (var theme in All)
        {
            theme.Validate();
        }

        Default = defaultTheme;
        _resolved = All.ToDictionary(theme => theme.Name, theme => ActiveTheme.From(theme, defaultTheme), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ThemeDefinition> All { get; }

    public ThemeDefinition Default { get; }

    public ActiveTheme? Find(string? name) =>
        name is not null && _resolved.TryGetValue(name, out var theme) ? theme : null;

    public ActiveTheme Resolve(string? name) => Find(name) ?? _resolved[Default.Name];
}

/// <summary>The store's theme, from configuration ("Loja:Theme"); the default theme when unset or unknown.</summary>
internal sealed class ThemeAccessor(ThemeCatalog catalog, IConfiguration configuration) : IThemeAccessor
{
    public ActiveTheme Current { get; } = catalog.Resolve(configuration[Themes.ConfigurationKey]);

    public IReadOnlyList<ThemeDefinition> All => catalog.All;
}

public static class Themes
{
    public const string ConfigurationKey = "Loja:Theme";

    /// <summary>Registers the store's themes; the first one is the default and must define every part.</summary>
    public static IServiceCollection AddLojaThemes(this IServiceCollection services, ThemeDefinition defaultTheme, params ThemeDefinition[] others)
    {
        services.AddSingleton(new ThemeCatalog(defaultTheme, others));
        services.AddScoped<IThemeAccessor, ThemeAccessor>();
        services.AddScoped<IStoreSessionProvider, StoreSessionProvider>();
        return services;
    }
}
