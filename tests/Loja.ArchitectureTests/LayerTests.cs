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
        { "Cart", typeof(Cart.Domain.ShoppingCart).Assembly },
    };

    public static TheoryData<string, Assembly> ApplicationAssemblies => new()
    {
        { "Catalog", typeof(Catalog.Application.Products.IProductQueries).Assembly },
        { "Cart", typeof(Cart.Application.ICartRepository).Assembly },
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

    [Theory]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_ShouldNotDependOn_InfrastructureOrFrameworks(string module, Assembly application)
    {
        var result = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOnAny(
                $"Loja.{module}.Infrastructure",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "StackExchange.Redis")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(TestResult result) =>
        $"Types with forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
