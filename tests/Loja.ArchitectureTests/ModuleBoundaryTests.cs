using System.Reflection;

namespace Loja.ArchitectureTests;

/// <summary>
/// Modular monolith rules: modules talk only through their Contracts.
/// </summary>
public class ModuleBoundaryTests
{
    public static TheoryData<string, string> InternalAssemblies()
    {
        var data = new TheoryData<string, string>();
        foreach (var (module, assembly) in Modules.InternalAssemblies)
        {
            data.Add(module, assembly.GetName().Name!);
        }

        return data;
    }

    public static TheoryData<string, string> ContractAssemblies()
    {
        var data = new TheoryData<string, string>();
        foreach (var (module, assembly) in Modules.ContractAssemblies)
        {
            data.Add(module, assembly.GetName().Name!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InternalAssemblies))]
    public void Module_ShouldNotUse_TheInsidesOfAnotherModule(string module, string assemblyName)
    {
        var forbidden = Modules.Names
            .Where(other => other != module)
            .SelectMany(Modules.InternalNamespacesOf)
            .ToArray();

        var result = Modules.CheckNoDependency(Load(assemblyName), forbidden);

        result.IsSuccessful.ShouldBeTrue(Modules.Describe(result));
    }

    [Theory]
    [MemberData(nameof(ContractAssemblies))]
    public void Contracts_ShouldNotExpose_TheirModuleInsides(string module, string assemblyName)
    {
        var result = Modules.CheckNoDependency(Load(assemblyName), Modules.InternalNamespacesOf(module));

        result.IsSuccessful.ShouldBeTrue(Modules.Describe(result));
    }

    [Fact]
    public void WebAndApi_ShouldNotUse_AnyDomain()
    {
        var domains = Modules.Names.Select(module => $"Loja.{module}.Domain").ToArray();

        foreach (var assembly in new[] { typeof(Program).Assembly, typeof(Api.ApiEndpoints).Assembly })
        {
            var result = Modules.CheckNoDependency(assembly, domains);
            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name}: {Modules.Describe(result)}");
        }
    }

    [Fact]
    public void Api_ShouldNotUse_AnyInfrastructure()
    {
        var infrastructure = Modules.Names.Select(module => $"Loja.{module}.Infrastructure").ToArray();

        var result = Modules.CheckNoDependency(typeof(Api.ApiEndpoints).Assembly, infrastructure);

        result.IsSuccessful.ShouldBeTrue(Modules.Describe(result));
    }

    // Theory data must be serializable, so tests receive the assembly name and load it here.
    private static Assembly Load(string assemblyName) => Assembly.Load(assemblyName);
}
