using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace Loja.ArchitectureTests;

public class SharedKernelTests
{
    // The shared kernel is referenced by every module's Domain, so it must stay framework-free.
    private static readonly string[] ForbiddenDependencies =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
    ];

    [Fact]
    public void SharedKernel_ShouldNotDependOn_InfrastructureFrameworks()
    {
        var result = Types.InAssembly(SharedKernel.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(TestResult result) =>
        $"Types with forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
