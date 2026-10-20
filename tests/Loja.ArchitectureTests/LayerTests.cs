using System.Reflection;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace Loja.ArchitectureTests;

/// <summary>
/// Clean Architecture rules for every module: dependencies point inwards.
/// </summary>
public class LayerTests
{
    // Add one entry per module as modules are created.
    public static TheoryData<string, Assembly> DomainAssemblies => new()
    {
        { "Catalog", typeof(Catalog.Domain.Products.Product).Assembly },
    };

    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_ShouldNotDependOn_OuterLayersOrFrameworks(string module, Assembly domain)
    {
        var result = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Loja.{module}.Application",
                $"Loja.{module}.Infrastructure",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(TestResult result) =>
        $"Types with forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
