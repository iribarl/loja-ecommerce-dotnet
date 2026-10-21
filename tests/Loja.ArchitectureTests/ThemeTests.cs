using System.Reflection;

namespace Loja.ArchitectureTests;

/// <summary>
/// Themes are presentation only: like the web host they may use the Application layer, never a
/// module's Domain or Infrastructure, and never the host itself (the host references them).
/// </summary>
public class ThemeTests
{
    // Add each new theme here.
    public static TheoryData<string> ThemeAssemblies => new()
    {
        typeof(Themes.ThemeDefinition).Assembly.GetName().Name!,
        typeof(Themes.Classico.ClassicoTheme).Assembly.GetName().Name!,
        typeof(Themes.Moderno.ModernoTheme).Assembly.GetName().Name!,
    };

    [Theory]
    [MemberData(nameof(ThemeAssemblies))]
    public void Theme_ShouldNotUse_DomainInfrastructureOrTheHost(string assemblyName)
    {
        var forbidden = Modules.Names
            .SelectMany(module => new[] { $"Loja.{module}.Domain", $"Loja.{module}.Infrastructure" })
            .Append("Loja.Web")
            .ToArray();

        var result = Modules.CheckNoDependency(Assembly.Load(assemblyName), forbidden);

        result.IsSuccessful.ShouldBeTrue(Modules.Describe(result));
    }

    [Fact]
    public void EveryTheme_IsWellFormed()
    {
        // Each part points at a component of the right kind (throws otherwise).
        Themes.Classico.ClassicoTheme.Definition.Validate();
        Themes.Moderno.ModernoTheme.Definition.Validate();
    }
}
